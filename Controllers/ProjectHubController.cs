using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    // Admin page "project hub": the links board, website monitoring results, and the Netlify drop zone
    // (drag the website folder in, it's zipped in the browser and deployed through Netlify's API).
    [ApiController]
    [Route("api/admin/hub")]
    public class ProjectHubController : ControllerBase
    {
        private static readonly HttpClient _netlify = new() { Timeout = TimeSpan.FromMinutes(3) };
        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _config;

        public ProjectHubController(UserManager<ApplicationUser> users, ApplicationDbContext db, IConfiguration config)
        {
            _users = users; _db = db; _config = config;
        }

        private class LinkRow { public int Id { get; set; } public string GroupName { get; set; } public string Name { get; set; } public string Url { get; set; } public string Note { get; set; } public int SortOrder { get; set; } }
        private class CheckRow { public string Target { get; set; } public DateTime CheckedAt { get; set; } public bool Ok { get; set; } public int? StatusCode { get; set; } public int Ms { get; set; } public string Error { get; set; } }
        public record LinkRequest(string Group, string Name, string Url, string Note);

        private async Task<ApplicationUser> RequireAdmin()
        {
            var u = await _users.GetUserAsync(User);
            return u != null && u.Administrator ? u : null;
        }
        private IActionResult Denied() => Unauthorized(new { ok = false, error = "Sign in as an administrator." });
        private bool IsOperator(ApplicationUser u) =>
            (_config["Production:Operators"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(u.UserName, StringComparer.OrdinalIgnoreCase);

        // ---------------- Links board ----------------

        [HttpGet("links")]
        public async Task<IActionResult> Links()
        {
            if (await RequireAdmin() == null) return Denied();
            var rows = await _db.Database.SqlQuery<LinkRow>($"SELECT Id, GroupName, Name, Url, Note, SortOrder FROM dbo.ProjectLinks ORDER BY GroupName, SortOrder, Name").ToListAsync();
            return Ok(new { ok = true, links = rows.Select(r => new { r.Id, group = r.GroupName, r.Name, r.Url, r.Note }) });
        }

        [HttpPost("links")]
        public async Task<IActionResult> AddLink([FromBody] LinkRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            if (string.IsNullOrWhiteSpace(req?.Name) || string.IsNullOrWhiteSpace(req.Url)) return BadRequest(new { ok = false, error = "Name and address are needed." });
            if (!Uri.TryCreate(req.Url.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != "https" && uri.Scheme != "http" && uri.Scheme != "mailto"))
                return BadRequest(new { ok = false, error = "The address must start with https://, http:// or mailto:." });
            var group = string.IsNullOrWhiteSpace(req.Group) ? "Other" : req.Group.Trim();
            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT dbo.ProjectLinks (GroupName, Name, Url, Note, SortOrder) VALUES ({group}, {req.Name.Trim()}, {req.Url.Trim()}, {req.Note?.Trim()}, 99)");
            return Ok(new { ok = true });
        }

        [HttpPut("links/{id:int}")]
        public async Task<IActionResult> EditLink(int id, [FromBody] LinkRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            if (string.IsNullOrWhiteSpace(req?.Name) || string.IsNullOrWhiteSpace(req.Url)) return BadRequest(new { ok = false, error = "Name and address are needed." });
            var group = string.IsNullOrWhiteSpace(req.Group) ? "Other" : req.Group.Trim();
            var n = await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.ProjectLinks SET GroupName = {group}, Name = {req.Name.Trim()}, Url = {req.Url.Trim()}, Note = {req.Note?.Trim()} WHERE Id = {id}");
            return n == 1 ? Ok(new { ok = true }) : NotFound(new { ok = false, error = "Link not found." });
        }

        [HttpDelete("links/{id:int}")]
        public async Task<IActionResult> DeleteLink(int id)
        {
            if (await RequireAdmin() == null) return Denied();
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.ProjectLinks WHERE Id = {id}");
            return Ok(new { ok = true });
        }

        // ---------------- Website monitoring ----------------

        [HttpGet("monitor")]
        public async Task<IActionResult> Monitor()
        {
            if (await RequireAdmin() == null) return Denied();
            var since = DateTime.UtcNow.AddHours(-24);
            var rows = await _db.Database.SqlQuery<CheckRow>($"SELECT Target, CheckedAt, Ok, StatusCode, Ms, Error FROM dbo.SiteChecks WHERE CheckedAt >= {since} ORDER BY CheckedAt").ToListAsync();
            var targets = rows.GroupBy(r => r.Target).Select(g =>
            {
                var list = g.ToList();
                var last = list[^1];
                var lastFail = list.LastOrDefault(r => !r.Ok);
                return new
                {
                    name = g.Key,
                    up = last.Ok,
                    statusCode = last.StatusCode,
                    ms = last.Ms,
                    error = last.Error,
                    checkedAt = last.CheckedAt,
                    uptime24h = Math.Round(100.0 * list.Count(r => r.Ok) / list.Count, 2),
                    avgMs = (int)list.Where(r => r.Ok).DefaultIfEmpty().Average(r => r?.Ms ?? 0),
                    checks = list.Count,
                    lastFailure = lastFail == null ? null : new { at = lastFail.CheckedAt, error = lastFail.Error },
                    recent = list.TakeLast(48).Select(r => new { r.Ok, r.Ms }),
                };
            }).ToList();
            var minutes = int.TryParse(_config["Monitoring:IntervalMinutes"], out var m) && m > 0 ? m : 5;
            return Ok(new { ok = true, intervalMinutes = minutes, targets });
        }

        // ---------------- Netlify deploy ----------------
        // The browser zips the site folder and posts it here; we hand it to Netlify's zip deploy API.

        private bool NetlifyConfigured => !string.IsNullOrWhiteSpace(_config["Netlify:Token"]) && !string.IsNullOrWhiteSpace(_config["Netlify:SiteId"]);

        private HttpRequestMessage NetlifyRequest(HttpMethod method, string path)
        {
            var req = new HttpRequestMessage(method, "https://api.netlify.com/api/v1/" + path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config["Netlify:Token"]);
            req.Headers.UserAgent.ParseAdd("SmartCubeMobile-Admin");
            return req;
        }

        [HttpGet("netlify/site")]
        public async Task<IActionResult> NetlifySite()
        {
            var user = await RequireAdmin();
            if (user == null) return Denied();
            if (!NetlifyConfigured) return Ok(new { ok = true, configured = false, canDeploy = IsOperator(user) });
            try
            {
                using var res = await _netlify.SendAsync(NetlifyRequest(HttpMethod.Get, $"sites/{_config["Netlify:SiteId"]}"));
                if (!res.IsSuccessStatusCode) return Ok(new { ok = true, configured = true, canDeploy = IsOperator(user), error = $"Netlify said {(int)res.StatusCode}. Check the token and site id." });
                var j = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
                var pub = j.TryGetProperty("published_deploy", out var pd) && pd.ValueKind == JsonValueKind.Object ? pd : default;
                return Ok(new
                {
                    ok = true, configured = true, canDeploy = IsOperator(user),
                    name = j.GetProperty("name").GetString(),
                    url = j.TryGetProperty("ssl_url", out var su) ? su.GetString() : j.GetProperty("url").GetString(),
                    adminUrl = j.TryGetProperty("admin_url", out var au) ? au.GetString() : null,
                    publishedAt = pub.ValueKind == JsonValueKind.Object && pub.TryGetProperty("published_at", out var pa) ? pa.GetString() : null,
                    publishedTitle = pub.ValueKind == JsonValueKind.Object && pub.TryGetProperty("title", out var pt) ? pt.GetString() : null,
                });
            }
            catch (Exception ex) { return Ok(new { ok = true, configured = true, canDeploy = IsOperator(user), error = ex.Message }); }
        }

        [HttpPost("netlify/deploy")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> NetlifyDeploy([FromQuery] string title)
        {
            var user = await RequireAdmin();
            if (user == null) return Denied();
            if (!IsOperator(user)) return StatusCode(403, new { ok = false, error = "Only the website owner can deploy it." });
            if (!NetlifyConfigured) return StatusCode(503, new { ok = false, error = "Netlify isn't set up: add Netlify:Token and Netlify:SiteId to the server's appsettings.json." });

            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            if (ms.Length < 100) return BadRequest(new { ok = false, error = "No zip received." });

            var req = NetlifyRequest(HttpMethod.Post, $"sites/{_config["Netlify:SiteId"]}/deploys" + (string.IsNullOrWhiteSpace(title) ? "" : "?title=" + Uri.EscapeDataString(title)));
            req.Content = new ByteArrayContent(ms.ToArray());
            req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
            using var res = await _netlify.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode) return StatusCode(502, new { ok = false, error = $"Netlify refused the deploy ({(int)res.StatusCode}): {body}" });
            var j = JsonDocument.Parse(body).RootElement;
            return Ok(new { ok = true, id = j.GetProperty("id").GetString(), state = j.GetProperty("state").GetString(), url = j.TryGetProperty("ssl_url", out var u) ? u.GetString() : null });
        }

        [HttpGet("netlify/deploy/{id}")]
        public async Task<IActionResult> NetlifyDeployStatus(string id)
        {
            if (await RequireAdmin() == null) return Denied();
            if (!NetlifyConfigured || id.Any(c => !char.IsLetterOrDigit(c))) return BadRequest(new { ok = false, error = "Bad request." });
            using var res = await _netlify.SendAsync(NetlifyRequest(HttpMethod.Get, $"deploys/{id}"));
            if (!res.IsSuccessStatusCode) return StatusCode(502, new { ok = false, error = $"Netlify said {(int)res.StatusCode}." });
            var j = JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement;
            return Ok(new
            {
                ok = true,
                state = j.GetProperty("state").GetString(),
                url = j.TryGetProperty("ssl_url", out var u) ? u.GetString() : null,
                deployUrl = j.TryGetProperty("deploy_ssl_url", out var du) ? du.GetString() : null,
                error = j.TryGetProperty("error_message", out var em) && em.ValueKind == JsonValueKind.String ? em.GetString() : null,
            });
        }
    }
}
