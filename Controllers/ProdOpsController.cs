using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    // Production operations from the admin page.
    //  - status: what's waiting on GitHub that production hasn't pulled (any admin can see it)
    //  - run:    queue a production script; only the operator accounts (appsettings Production:Operators)
    // The scripts don't run here: run-jobs.ps1, a scheduled task on the production PC, polls dbo.ProdJobs
    // every minute and runs them as the signed-in Windows user, then writes the output back.
    [ApiController]
    [Route("api/admin/ops")]
    public class ProdOpsController : ControllerBase
    {
        private static readonly HttpClient _gh = new() { Timeout = TimeSpan.FromSeconds(20) };
        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public ProdOpsController(UserManager<ApplicationUser> users, ApplicationDbContext db, IConfiguration config)
        {
            _users = users; _db = db; _config = config;
        }

        public record RunRequest(string Job, string Version, string Notes);
        private class JobRow { public int Id { get; set; } public string Job { get; set; } public string Args { get; set; } public string RequestedBy { get; set; } public DateTime RequestedAt { get; set; } public DateTime? StartedAt { get; set; } public DateTime? FinishedAt { get; set; } public string Status { get; set; } public string Output { get; set; } }
        private class StateRow { public string Part { get; set; } public string Sha { get; set; } public bool Dirty { get; set; } public DateTime RunnerSeenAt { get; set; } }

        private static readonly string[] Jobs = { "pull-app", "pull-server", "deploy-server", "release" };

        private async Task<ApplicationUser> RequireAdmin()
        {
            var u = await _users.GetUserAsync(User);
            return u != null && u.Administrator ? u : null;
        }
        private bool IsOperator(ApplicationUser u) =>
            (_config["Production:Operators"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(u.UserName, StringComparer.OrdinalIgnoreCase);

        [HttpGet("status")]
        public async Task<IActionResult> Status()
        {
            var user = await RequireAdmin();
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in as an administrator." });

            var state = await _db.Database.SqlQuery<StateRow>($"SELECT Part, Sha, Dirty, RunnerSeenAt FROM dbo.ProdState").ToListAsync();
            var jobs = await _db.Database.SqlQuery<JobRow>($"SELECT TOP 12 Id, Job, Args, RequestedBy, RequestedAt, StartedAt, FinishedAt, Status, Output FROM dbo.ProdJobs ORDER BY Id DESC").ToListAsync();
            var runnerSeen = state.Count > 0 ? state.Max(s => s.RunnerSeenAt) : (DateTime?)null;

            return Ok(new
            {
                ok = true,
                isOperator = IsOperator(user),
                runnerAlive = runnerSeen.HasValue && runnerSeen.Value > DateTime.UtcNow.AddMinutes(-3),
                runnerSeen,
                app = await Waiting("app", _config["GitHub:AppRepo"] ?? "smartcube-app", state),
                server = await Waiting("server", _config["GitHub:ServerRepo"] ?? "smartcube-server", state),
                jobs = jobs.Select(j => new { j.Id, j.Job, j.Args, j.RequestedBy, j.RequestedAt, j.StartedAt, j.FinishedAt, j.Status, output = j.Output }),
            });
        }

        [HttpPost("run")]
        public async Task<IActionResult> Run([FromBody] RunRequest req)
        {
            var user = await RequireAdmin();
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in as an administrator." });
            if (!IsOperator(user)) return StatusCode(403, new { ok = false, error = "Only the production operator can run these." });
            if (req?.Job == null || !Jobs.Contains(req.Job)) return BadRequest(new { ok = false, error = "Unknown job." });

            string args = null;
            if (req.Job == "release")
            {
                if (string.IsNullOrWhiteSpace(req.Version) || !System.Text.RegularExpressions.Regex.IsMatch(req.Version.Trim(), @"^\d+\.\d+(\.\d+)?$"))
                    return BadRequest(new { ok = false, error = "Give a version like 1.0.9." });
                if (string.IsNullOrWhiteSpace(req.Notes)) return BadRequest(new { ok = false, error = "Give release notes (what's new for users)." });
                args = JsonSerializer.Serialize(new { version = req.Version.Trim(), notes = req.Notes.Trim() });
            }

            var pending = await _db.Database.SqlQuery<JobRow>($"SELECT Id, Job, Args, RequestedBy, RequestedAt, StartedAt, FinishedAt, Status, Output FROM dbo.ProdJobs WHERE Status IN ('queued','running')").ToListAsync();
            if (pending.Count > 0) return Conflict(new { ok = false, error = $"'{pending[0].Job}' is still {pending[0].Status}. Wait for it to finish." });

            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.ProdJobs (Job, Args, RequestedBy) VALUES ({req.Job}, {args}, {user.UserName})");
            var (woke, detail) = WakeRunner();
            return Ok(new { ok = true, woke, detail });
        }

        // Start the runner now instead of waiting for its hourly schedule (admin page Refresh, and after
        // queueing a job). Needs the app pool to be allowed to run the task:
        //   icacls C:\Windows\System32\Tasks\SmartCube-ProdRunner /grant "IIS APPPOOL\DefaultAppPool:RX"
        [HttpPost("wake")]
        public async Task<IActionResult> Wake()
        {
            if (await RequireAdmin() == null) return Unauthorized(new { ok = false, error = "Sign in as an administrator." });
            var (ok, detail) = WakeRunner();
            return Ok(new { ok = true, woke = ok, detail });
        }

        private (bool Ok, string Detail) WakeRunner()
        {
            try
            {
                var task = _config["Production:RunnerTask"] ?? "SmartCube-ProdRunner";
                var psi = new System.Diagnostics.ProcessStartInfo("schtasks.exe", $"/run /tn \"{task}\"")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using var p = System.Diagnostics.Process.Start(psi);
                var output = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
                p.WaitForExit(10000);
                return (p.ExitCode == 0, output.Trim());
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        // GitHub compare: production's commit ... main. ahead_by = drops not yet pulled.
        private async Task<object> Waiting(string part, string repo, List<StateRow> state)
        {
            var s = state.FirstOrDefault(x => x.Part == part);
            if (s?.Sha == null) return new { known = false, error = "Production hasn't reported yet (is run-jobs.ps1 scheduled?)." };
            var token = _config["GitHub:Token"];
            if (string.IsNullOrWhiteSpace(token)) return new { known = false, error = "No GitHub token on the server." };
            try
            {
                var owner = _config["GitHub:Owner"] ?? "SmartCubeMobile";
                var branch = _config["GitHub:Branch"] ?? "main";
                var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/compare/{s.Sha}...{branch}?per_page=50");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                req.Headers.UserAgent.ParseAdd("SmartCubeMobile-Admin");
                req.Headers.Accept.ParseAdd("application/vnd.github+json");
                using var res = await _gh.SendAsync(req);
                if (!res.IsSuccessStatusCode)
                    return new { known = false, prodSha = s.Sha, error = res.StatusCode == System.Net.HttpStatusCode.NotFound
                        ? "Production has a commit GitHub doesn't know about (an unpushed hot-fix?)." : $"GitHub compare failed ({(int)res.StatusCode})." };
                var j = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
                var commits = j.GetProperty("commits").EnumerateArray().Select(c => new
                {
                    sha = c.GetProperty("sha").GetString()[..7],
                    url = c.GetProperty("html_url").GetString(),
                    message = c.GetProperty("commit").GetProperty("message").GetString().Split('\n')[0],
                    author = c.GetProperty("commit").GetProperty("author").GetProperty("name").GetString(),
                    date = c.GetProperty("commit").GetProperty("author").GetProperty("date").GetString(),
                }).Reverse().ToList();
                return new { known = true, prodSha = s.Sha, dirty = s.Dirty, aheadBy = j.GetProperty("ahead_by").GetInt32(), behindBy = j.GetProperty("behind_by").GetInt32(), commits };
            }
            catch (Exception ex) { return new { known = false, prodSha = s.Sha, error = ex.Message }; }
        }
    }
}
