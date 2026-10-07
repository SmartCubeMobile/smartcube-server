using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    // What the production backup job (Backup-SmartCube.ps1, a scheduled task on the production PC)
    // has done lately, for the admin page's Production section. Read-only.
    [ApiController]
    [Route("api/admin/backups")]
    public class BackupStatusController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;

        public BackupStatusController(UserManager<ApplicationUser> users, ApplicationDbContext db)
        {
            _users = users; _db = db;
        }

        private class RunRow { public DateTime At { get; set; } public string Kind { get; set; } public bool Ok { get; set; } public long Bytes { get; set; } public string FileName { get; set; } public string Message { get; set; } }

        [HttpGet("")]
        public async Task<IActionResult> Status()
        {
            var u = await _users.GetUserAsync(User);
            if (u == null || !u.Administrator) return Unauthorized(new { ok = false, error = "Sign in as an administrator." });

            var rows = await _db.Database.SqlQuery<RunRow>($"SELECT TOP 400 At, Kind, Ok, Bytes, FileName, Message FROM dbo.BackupRuns ORDER BY Id DESC").ToListAsync();
            object Last(string kind, bool? ok = null)
            {
                var r = rows.FirstOrDefault(x => x.Kind == kind && (ok == null || x.Ok == ok));
                return r == null ? null : new { r.At, r.Ok, r.Bytes, r.FileName, r.Message };
            }
            var since = DateTime.UtcNow.AddHours(-24);
            var lastFull = rows.FirstOrDefault(x => x.Kind == "full" && x.Ok);
            var lastError = rows.FirstOrDefault(x => x.Kind == "error" || !x.Ok);
            return Ok(new
            {
                ok = true,
                lastFull = Last("full", true),
                lastLog = Last("log", true),
                lastRestoreTest = Last("restore-test"),
                lastProblem = lastError == null ? null : new { lastError.At, lastError.Kind, lastError.Message },
                last24h = new { fulls = rows.Count(x => x.Kind == "full" && x.Ok && x.At >= since), logs = rows.Count(x => x.Kind == "log" && x.Ok && x.At >= since), failures = rows.Count(x => !x.Ok && x.At >= since) },
                // The job only runs while the production PC is on and signed in; "stale" means it hasn't for over 3 hours.
                stale = lastFull == null || lastFull.At < DateTime.UtcNow.AddHours(-3),
            });
        }
    }
}
