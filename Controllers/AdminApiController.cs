using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobile;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    [ApiController]
    [Route("api/admin")]
    public class AdminApiController : ControllerBase
    {
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(8) };

        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IConfiguration _config;

        public AdminApiController(UserManager<ApplicationUser> users, ApplicationDbContext db,
            IWebHostEnvironment env, IConfiguration config)
        {
            _users = users;
            _db = db;
            _env = env;
            _config = config;
        }

        public record PlanRequest(string Plan, int Months);
        public record LicenceRequest(string Action);
        public record ActiveRequest(bool Active);

        private class LicenceRow { public string LicenceKey { get; set; } public string Email { get; set; } public bool IsActive { get; set; } public int ScansUsed { get; set; } public int ScanLimit { get; set; } public DateTime? Expires { get; set; } public DateTime? LastUsed { get; set; } }
        private class DeviceRow { public string UserId { get; set; } public string DeviceName { get; set; } public DateTime Created { get; set; } public DateTime? LastUsed { get; set; } }
        private class ScanRow { public int Id { get; set; } public string LicenceKey { get; set; } public string Category { get; set; } public int InputChars { get; set; } public bool Success { get; set; } public string Error { get; set; } public DateTime ScannedAt { get; set; } }
        private class LoginRow { public int Id { get; set; } public string Login { get; set; } public string UserId { get; set; } public string Method { get; set; } public bool Success { get; set; } public string Error { get; set; } public string AppVersion { get; set; } public string IP { get; set; } public DateTime At { get; set; } }
        private class CountRow { public int Value { get; set; } }
        private class VersionRow { public string UserId { get; set; } public string AppVersion { get; set; } }

        private ApplicationUser _caller;

        private async Task<ApplicationUser> RequireAdmin()
        {
            _caller = await _users.GetUserAsync(User);
            return _caller != null && _caller.Administrator ? _caller : null;
        }

        private IActionResult Denied() =>
            _caller != null
                ? StatusCode(403, new { ok = false, error = "Administrator access required." })
                : Unauthorized(new { ok = false, error = "Sign in as an administrator." });

        [HttpGet("overview")]
        public async Task<IActionResult> Overview()
        {
            if (await RequireAdmin() == null) return Denied();

            var now = DateTime.UtcNow;
            var users = await _db.Users.ToListAsync();
            var planClaims = await _db.UserClaims.Where(c => c.ClaimType == "plan").ToListAsync();
            var plans = planClaims.GroupBy(c => c.ClaimValue).ToDictionary(g => g.Key, g => g.Count());

            var scansToday = await Count($"SELECT COUNT(*) AS Value FROM dbo.SmartScanLog WHERE ScannedAt >= {now.Date}");
            var scansMonth = await Count($"SELECT COUNT(*) AS Value FROM dbo.SmartScanLog WHERE ScannedAt >= {new DateTime(now.Year, now.Month, 1)}");
            var scansMonthOk = await Count($"SELECT COUNT(*) AS Value FROM dbo.SmartScanLog WHERE Success = 1 AND ScannedAt >= {new DateTime(now.Year, now.Month, 1)}");
            var scansFailed7d = await Count($"SELECT COUNT(*) AS Value FROM dbo.SmartScanLog WHERE Success = 0 AND ScannedAt >= {now.AddDays(-7)}");
            var downloads = await Count($"SELECT COUNT(*) AS Value FROM dbo.DownloadLog");
            var downloads7d = await Count($"SELECT COUNT(*) AS Value FROM dbo.DownloadLog WHERE At >= {now.AddDays(-7)}");
            var failedLogins24h = await Count($"SELECT COUNT(*) AS Value FROM dbo.LoginLog WHERE Success = 0 AND At >= {now.AddHours(-24)}");
            var licences = await Count($"SELECT COUNT(*) AS Value FROM dbo.SmartScanLicences WHERE IsActive = 1");

            var installer = FindInstaller();

            return Ok(new
            {
                ok = true,
                users = new
                {
                    total = users.Count,
                    newLast7d = users.Count(u => u.Expiration3 >= now.AddDays(-7) && !u.Subscriber),
                    activeLast7d = users.Count(u => u.LastLogOnTime >= now.AddDays(-7)),
                    activeLast30d = users.Count(u => u.LastLogOnTime >= now.AddDays(-30)),
                    subscribers = users.Count(u => u.Subscriber),
                    locked = users.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow),
                    admins = users.Count(u => u.Administrator),
                },
                plans = new
                {
                    basic = plans.GetValueOrDefault("Basic"),
                    pro = plans.GetValueOrDefault("Pro"),
                    free = users.Count - planClaims.Select(c => c.UserId).Distinct().Count(),
                },
                scans = new
                {
                    today = scansToday,
                    thisMonth = scansMonth,
                    thisMonthOk = scansMonthOk,
                    failedLast7d = scansFailed7d,
                    estimatedCostGbp = Math.Round(scansMonthOk * 0.01m, 2),
                    activeLicences = licences,
                },
                downloads = new { total = downloads, last7d = downloads7d },
                logins = new { failedLast24h = failedLogins24h },
                installer = installer == null ? null : new
                {
                    fileName = installer.Name,
                    version = System.Text.RegularExpressions.Regex.Match(installer.Name, @"\d+(?:\.\d+){1,3}").Value,
                    sizeMb = Math.Round(installer.Length / 1048576.0, 1),
                    updated = installer.LastWriteTimeUtc,
                },
                serverTimeUtc = now,
            });
        }

        [HttpGet("users")]
        public async Task<IActionResult> Users([FromQuery] string q)
        {
            if (await RequireAdmin() == null) return Denied();

            var query = _db.Users.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(u => u.UserName.Contains(q) || u.Email.Contains(q));
            }
            var users = await query.OrderByDescending(u => u.LastLogOnTime).Take(500).ToListAsync();
            var ids = users.Select(u => u.Id).ToList();
            var claims = await _db.UserClaims.Where(c => ids.Contains(c.UserId)).ToListAsync();
            var licences = await _db.Database.SqlQuery<LicenceRow>($"SELECT LicenceKey, Email, IsActive, ScansUsed, ScanLimit, Expires, LastUsed FROM dbo.SmartScanLicences WHERE IsActive = 1").ToListAsync();
            var devices = await _db.Database.SqlQuery<DeviceRow>($"SELECT UserId, DeviceName, Created, LastUsed FROM dbo.DeviceTokens WHERE Revoked = 0").ToListAsync();
            var versions = await _db.Database.SqlQuery<VersionRow>($"SELECT UserId, AppVersion FROM (SELECT UserId, AppVersion, ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY At DESC) AS rn FROM dbo.LoginLog WHERE Success = 1 AND AppVersion IS NOT NULL) x WHERE rn = 1").ToListAsync();

            var rows = users.Select(u =>
            {
                var lic = licences.FirstOrDefault(l => string.Equals(l.Email, u.Email, StringComparison.OrdinalIgnoreCase));
                return new
                {
                    id = u.Id,
                    username = u.UserName,
                    email = u.Email,
                    fullName = claims.FirstOrDefault(c => c.UserId == u.Id && c.ClaimType == "full_name")?.ClaimValue,
                    plan = claims.FirstOrDefault(c => c.UserId == u.Id && c.ClaimType == "plan")?.ClaimValue ?? "Free",
                    subscriber = u.Subscriber,
                    expires = u.Expiration1,
                    created = u.Expiration3,
                    lastLogin = u.LastLogOnTime == default ? (DateTime?)null : u.LastLogOnTime,
                    locked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow,
                    admin = u.Administrator,
                    mustChangePassword = claims.Any(c => c.UserId == u.Id && c.ClaimType == "must_change_password"),
                    appVersion = versions.FirstOrDefault(v => v.UserId == u.Id)?.AppVersion,
                    devices = devices.Count(d => d.UserId == u.Id),
                    licence = lic == null ? null : new { key = lic.LicenceKey, used = lic.ScansUsed, limit = lic.ScanLimit, expires = lic.Expires, lastUsed = lic.LastUsed },
                };
            });
            return Ok(new { ok = true, users = rows });
        }

        [HttpGet("user/{id}")]
        public async Task<IActionResult> UserDetail(string id)
        {
            if (await RequireAdmin() == null) return Denied();
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound(new { ok = false, error = "User not found." });

            var devices = await _db.Database.SqlQuery<DeviceRow>($"SELECT UserId, DeviceName, Created, LastUsed FROM dbo.DeviceTokens WHERE Revoked = 0 AND UserId = {id} ORDER BY Created DESC").ToListAsync();
            var logins = await _db.Database.SqlQuery<LoginRow>($"SELECT TOP 20 Id, Login, UserId, Method, Success, Error, AppVersion, IP, At FROM dbo.LoginLog WHERE UserId = {id} OR Login = {u.UserName} OR Login = {u.Email} ORDER BY At DESC").ToListAsync();
            var licence = await _db.Database.SqlQuery<LicenceRow>($"SELECT LicenceKey, Email, IsActive, ScansUsed, ScanLimit, Expires, LastUsed FROM dbo.SmartScanLicences WHERE Email = {u.Email} ORDER BY Created DESC").ToListAsync();
            var scans = licence.Count == 0 ? new List<ScanRow>() :
                await _db.Database.SqlQuery<ScanRow>($"SELECT TOP 20 Id, LicenceKey, Category, InputChars, Success, Error, ScannedAt FROM dbo.SmartScanLog WHERE LicenceKey = {licence[0].LicenceKey} ORDER BY ScannedAt DESC").ToListAsync();

            return Ok(new
            {
                ok = true,
                devices = devices.Select(d => new { d.DeviceName, d.Created, d.LastUsed }),
                logins = logins.Select(l => new { l.Method, l.Success, l.Error, l.AppVersion, l.IP, l.At }),
                licences = licence.Select(l => new { key = l.LicenceKey, l.IsActive, used = l.ScansUsed, limit = l.ScanLimit, l.Expires, l.LastUsed }),
                scans = scans.Select(s => new { s.Category, s.InputChars, s.Success, s.Error, s.ScannedAt }),
            });
        }

        [HttpPost("user/{id}/plan")]
        public async Task<IActionResult> SetPlan(string id, [FromBody] PlanRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound(new { ok = false, error = "User not found." });

            var claims = await _users.GetClaimsAsync(u);
            var existing = claims.FirstOrDefault(c => c.Type == "plan");
            if (existing != null) await _users.RemoveClaimAsync(u, existing);

            if (string.IsNullOrEmpty(req.Plan) || req.Plan == "Free")
            {
                u.Subscriber = false;
                u.Expiration1 = u.Expiration2 = DateTime.UtcNow;
            }
            else
            {
                var months = req.Months <= 0 ? 12 : req.Months;
                var baseDate = u.Subscriber && u.Expiration1 > DateTime.UtcNow ? u.Expiration1 : DateTime.UtcNow;
                u.Subscriber = true;
                u.Expiration1 = u.Expiration2 = baseDate.AddMonths(months);
                await _users.AddClaimAsync(u, new Claim("plan", req.Plan));
            }
            await _users.UpdateAsync(u);
            return Ok(new { ok = true, plan = req.Plan ?? "Free", expires = u.Expiration1 });
        }

        [HttpPost("user/{id}/licence")]
        public async Task<IActionResult> Licence(string id, [FromBody] LicenceRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound(new { ok = false, error = "User not found." });

            if (req.Action == "revoke")
            {
                await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SmartScanLicences SET IsActive = 0 WHERE Email = {u.Email}");
                return Ok(new { ok = true, licence = (string)null });
            }

            var key = NewLicenceKey();
            var expires = u.Expiration1 > DateTime.UtcNow ? u.Expiration1 : DateTime.UtcNow.AddYears(1);
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SmartScanLicences SET IsActive = 0 WHERE Email = {u.Email}");
            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.SmartScanLicences (LicenceKey, Email, ScanLimit, Expires) VALUES ({key}, {u.Email}, 100, {expires})");
            return Ok(new { ok = true, licence = key, expires });
        }

        [HttpPost("user/{id}/devices/forget")]
        public async Task<IActionResult> ForgetDevices(string id)
        {
            if (await RequireAdmin() == null) return Denied();
            var n = await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.DeviceTokens SET Revoked = 1 WHERE UserId = {id} AND Revoked = 0");
            return Ok(new { ok = true, revoked = n });
        }

        [HttpPost("user/{id}/active")]
        public async Task<IActionResult> SetActive(string id, [FromBody] ActiveRequest req)
        {
            var admin = await RequireAdmin();
            if (admin == null) return Denied();
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound(new { ok = false, error = "User not found." });
            if (u.Id == admin.Id && !req.Active) return BadRequest(new { ok = false, error = "You can't deactivate your own account." });

            u.LockoutEnabled = true;
            u.LockoutEnd = req.Active ? null : DateTimeOffset.UtcNow.AddYears(100);
            await _users.UpdateAsync(u);
            if (!req.Active)
                await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.DeviceTokens SET Revoked = 1 WHERE UserId = {id}");
            return Ok(new { ok = true, active = req.Active });
        }

        [HttpPost("user/{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (await RequireAdmin() == null) return Denied();
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound(new { ok = false, error = "User not found." });

            var temp = TempPassword();
            var token = await _users.GeneratePasswordResetTokenAsync(u);
            var result = await _users.ResetPasswordAsync(u, token, temp);
            if (!result.Succeeded)
                return BadRequest(new { ok = false, error = string.Join(" ", result.Errors.Select(e => e.Description)) });
            var claims = await _users.GetClaimsAsync(u);
            if (!claims.Any(c => c.Type == "must_change_password"))
                await _users.AddClaimAsync(u, new Claim("must_change_password", "1"));
            return Ok(new { ok = true, temporaryPassword = temp });
        }

        [HttpGet("scans")]
        public async Task<IActionResult> Scans([FromQuery] int take = 100)
        {
            if (await RequireAdmin() == null) return Denied();
            take = Math.Clamp(take, 1, 500);
            var rows = await _db.Database.SqlQuery<ScanRow>($"SELECT TOP ({take}) s.Id, s.LicenceKey, s.Category, s.InputChars, s.Success, s.Error, s.ScannedAt FROM dbo.SmartScanLog s ORDER BY s.ScannedAt DESC").ToListAsync();
            var licences = await _db.Database.SqlQuery<LicenceRow>($"SELECT LicenceKey, Email, IsActive, ScansUsed, ScanLimit, Expires, LastUsed FROM dbo.SmartScanLicences").ToListAsync();
            return Ok(new
            {
                ok = true,
                scans = rows.Select(s => new
                {
                    s.Id, s.LicenceKey, s.Category, s.InputChars, s.Success, s.Error, s.ScannedAt,
                    email = licences.FirstOrDefault(l => l.LicenceKey == s.LicenceKey)?.Email,
                })
            });
        }

        [HttpGet("logins")]
        public async Task<IActionResult> Logins([FromQuery] int take = 100, [FromQuery] bool failedOnly = false)
        {
            if (await RequireAdmin() == null) return Denied();
            take = Math.Clamp(take, 1, 500);
            var rows = failedOnly
                ? await _db.Database.SqlQuery<LoginRow>($"SELECT TOP ({take}) Id, Login, UserId, Method, Success, Error, AppVersion, IP, At FROM dbo.LoginLog WHERE Success = 0 ORDER BY At DESC").ToListAsync()
                : await _db.Database.SqlQuery<LoginRow>($"SELECT TOP ({take}) Id, Login, UserId, Method, Success, Error, AppVersion, IP, At FROM dbo.LoginLog ORDER BY At DESC").ToListAsync();
            return Ok(new { ok = true, logins = rows });
        }

        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            if (await RequireAdmin() == null) return Denied();

            string db = "ok";
            try { await _db.Database.ExecuteSqlRawAsync("SELECT 1"); } catch (Exception ex) { db = "error: " + ex.Message; }

            string tunnel = "unknown";
            try
            {
                var r = await _http.GetStringAsync("http://127.0.0.1:20241/ready");
                var m = System.Text.RegularExpressions.Regex.Match(r, "\"readyConnections\":(\\d+)");
                tunnel = m.Success ? $"ok ({m.Groups[1].Value} connections)" : "responding";
            }
            catch { tunnel = "not reachable (cloudflared metrics on 127.0.0.1:20241)"; }

            string claude;
            var key = _config["Claude:ApiKey"];
            if (string.IsNullOrEmpty(key)) claude = "no API key configured";
            else
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/v1/models?limit=1");
                    req.Headers.Add("x-api-key", key);
                    req.Headers.Add("anthropic-version", "2023-06-01");
                    using var resp = await _http.SendAsync(req);
                    claude = resp.IsSuccessStatusCode ? "key valid" : $"key rejected ({(int)resp.StatusCode})";
                }
                catch (Exception ex) { claude = "unreachable: " + ex.Message; }
            }

            var lastScanError = await _db.Database.SqlQuery<ScanRow>($"SELECT TOP 1 Id, LicenceKey, Category, InputChars, Success, Error, ScannedAt FROM dbo.SmartScanLog WHERE Success = 0 ORDER BY ScannedAt DESC").FirstOrDefaultAsync();
            if (lastScanError?.Error?.Contains("credit", StringComparison.OrdinalIgnoreCase) == true)
                claude += " - last scan failed: no credit";

            return Ok(new
            {
                ok = true,
                database = db,
                tunnel,
                claude,
                model = _config["Claude:Model"],
                lastScanError = lastScanError == null ? null : new { lastScanError.Error, lastScanError.ScannedAt },
                installer = FindInstaller()?.Name,
                serverTimeUtc = DateTime.UtcNow,
                machine = Environment.MachineName,
            });
        }

        // ---------------- Releases ----------------

        private class ReleaseRow { public int Id { get; set; } public string Version { get; set; } public string Notes { get; set; } public string FileName { get; set; } public decimal? SizeMb { get; set; } public DateTime ReleasedAt { get; set; } }
        private class NameCountRow { public string Name { get; set; } public int Value { get; set; } }

        [HttpGet("releases")]
        public async Task<IActionResult> Releases()
        {
            if (await RequireAdmin() == null) return Denied();

            var releases = await _db.Database.SqlQuery<ReleaseRow>($"SELECT Id, Version, Notes, FileName, SizeMb, ReleasedAt FROM dbo.Releases ORDER BY ReleasedAt DESC").ToListAsync();
            var downloads = await _db.Database.SqlQuery<NameCountRow>($"SELECT FileName AS Name, COUNT(*) AS Value FROM dbo.DownloadLog GROUP BY FileName").ToListAsync();
            var adoption = await _db.Database.SqlQuery<NameCountRow>($"SELECT AppVersion AS Name, COUNT(*) AS Value FROM (SELECT UserId, AppVersion, ROW_NUMBER() OVER (PARTITION BY UserId ORDER BY At DESC) AS rn FROM dbo.LoginLog WHERE Success = 1 AND AppVersion IS NOT NULL) x WHERE rn = 1 GROUP BY AppVersion").ToListAsync();
            var installer = FindInstaller();

            return Ok(new
            {
                ok = true,
                current = installer == null ? null : System.Text.RegularExpressions.Regex.Match(installer.Name, @"\d+(?:\.\d+){1,3}").Value,
                releases = releases.Select(r => new
                {
                    r.Id, r.Version, r.Notes, r.FileName, r.SizeMb, r.ReleasedAt,
                    available = FindReleaseFile(r.Version, r.FileName) != null,
                    sourceMb = FindSourceFile(r.Version) is { } src ? Math.Round(src.Length / 1048576.0, 1) : (double?)null,
                    downloads = downloads.FirstOrDefault(d => d.Name == r.FileName)?.Value ?? 0,
                    usersOnVersion = adoption.FirstOrDefault(a => a.Name == r.Version || a.Name == r.Version + ".0")?.Value ?? 0,
                }),
                adoption = adoption.Select(a => new { version = a.Name, users = a.Value }),
            });
        }

        // Developer download of any release's installer. The website's Installers folder only keeps the
        // newest one, so older versions come from the release archive (appsettings Releases:ArchiveDir,
        // default D:\SmartCubeMobile\Versions). Not written to DownloadLog, so user stats stay clean.
        [HttpGet("releases/{version}/download")]
        public async Task<IActionResult> DownloadRelease(string version)
        {
            if (await RequireAdmin() == null) return Denied();
            var fileName = (await _db.Database.SqlQuery<ReleaseRow>($"SELECT Id, Version, Notes, FileName, SizeMb, ReleasedAt FROM dbo.Releases WHERE Version = {version}").FirstOrDefaultAsync())?.FileName;
            var file = FindReleaseFile(version, fileName);
            if (file == null) return NotFound(new { ok = false, error = $"No installer found for v{version}." });
            return PhysicalFile(file.FullName, "application/octet-stream", file.Name, enableRangeProcessing: true);
        }

        // Developer source code for a release: a zip of the app's git tag (with setup notes), made by
        // make-source.ps1 during release.ps1 into {ArchiveDir}\Source. Opens in Visual Studio 2022.
        [HttpGet("releases/{version}/source")]
        public async Task<IActionResult> DownloadSource(string version)
        {
            if (await RequireAdmin() == null) return Denied();
            var file = FindSourceFile(version);
            if (file == null) return NotFound(new { ok = false, error = $"No source zip found for v{version}." });
            return PhysicalFile(file.FullName, "application/zip", file.Name, enableRangeProcessing: true);
        }

        private FileInfo FindSourceFile(string version)
        {
            if (string.IsNullOrWhiteSpace(version) || version.Any(c => !(char.IsDigit(c) || c == '.'))) return null;
            var p = Path.Combine(_config["Releases:ArchiveDir"] ?? @"D:\SmartCubeMobile\Versions", "Source", $"SmartCubeMobile-{version}-source.zip");
            return System.IO.File.Exists(p) ? new FileInfo(p) : null;
        }

        private FileInfo FindReleaseFile(string version, string fileName)
        {
            if (string.IsNullOrWhiteSpace(version) || version.Any(c => !(char.IsDigit(c) || c == '.'))) return null;
            var names = new List<string>();
            if (!string.IsNullOrWhiteSpace(fileName)) names.Add(Path.GetFileName(fileName));
            names.Add($"SmartCubeMobile-{version}-Setup.exe");
            var dirs = new[]
            {
                Path.Combine(_env.ContentRootPath, "Installers"),
                _config["Releases:ArchiveDir"] ?? @"D:\SmartCubeMobile\Versions",
            };
            foreach (var d in dirs)
                foreach (var n in names)
                {
                    var p = Path.Combine(d, n);
                    if (System.IO.File.Exists(p)) return new FileInfo(p);
                }
            return null;
        }

        // ---------------- Developer notes ----------------

        private class NoteRow { public int Id { get; set; } public string Type { get; set; } public string Title { get; set; } public string Body { get; set; } public string Status { get; set; } public string Priority { get; set; } public string AppVersion { get; set; } public string CreatedBy { get; set; } public DateTime CreatedAt { get; set; } public string UpdatedBy { get; set; } public DateTime? UpdatedAt { get; set; } public string PreviousBody { get; set; } }
        public record NoteRequest(string Type, string Title, string Body, string Priority, string AppVersion, string Status);

        private static readonly string[] NoteTypes = { "Task", "Bug", "Suggestion", "Fix", "Website", "Note" };
        private static readonly string[] NoteStatuses = { "Open", "In progress", "Done", "Won't fix" };
        private static readonly string[] NotePriorities = { "Low", "Normal", "High", "Urgent" };

        [HttpGet("notes")]
        public async Task<IActionResult> Notes([FromQuery] string status, [FromQuery] string type)
        {
            if (await RequireAdmin() == null) return Denied();
            // PreviousBody = the body as it was before the most recent edit, so the page can highlight what was added.
            var rows = await _db.Database.SqlQuery<NoteRow>($"SELECT n.Id, n.Type, n.Title, n.Body, n.Status, n.Priority, n.AppVersion, n.CreatedBy, n.CreatedAt, n.UpdatedBy, n.UpdatedAt, r.Body AS PreviousBody FROM dbo.DevNotes n OUTER APPLY (SELECT TOP 1 x.Body FROM dbo.DevNoteRevisions x WHERE x.NoteId = n.Id ORDER BY x.Id DESC) r ORDER BY CASE n.Status WHEN 'Open' THEN 0 WHEN 'In progress' THEN 1 ELSE 2 END, CASE n.Priority WHEN 'Urgent' THEN 0 WHEN 'High' THEN 1 WHEN 'Normal' THEN 2 ELSE 3 END, n.CreatedAt DESC").ToListAsync();
            if (!string.IsNullOrEmpty(status) && status != "All") rows = rows.Where(r => r.Status == status).ToList();
            if (!string.IsNullOrEmpty(type) && type != "All") rows = rows.Where(r => r.Type == type).ToList();
            return Ok(new { ok = true, notes = rows, types = NoteTypes, statuses = NoteStatuses, priorities = NotePriorities });
        }

        [HttpPost("notes")]
        public async Task<IActionResult> AddNote([FromBody] NoteRequest req)
        {
            var admin = await RequireAdmin();
            if (admin == null) return Denied();
            var title = (req.Title ?? "").Trim();
            if (title.Length == 0) return BadRequest(new { ok = false, error = "Title is required." });
            var type = NoteTypes.Contains(req.Type) ? req.Type : "Note";
            var priority = NotePriorities.Contains(req.Priority) ? req.Priority : "Normal";
            var installer = FindInstaller();
            var version = installer == null ? null
                : System.Text.RegularExpressions.Regex.Match(installer.Name, @"\d+(?:\.\d+){1,3}").Value;

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO dbo.DevNotes (Type, Title, Body, Priority, AppVersion, CreatedBy) VALUES ({type}, {title}, {(object)(req.Body ?? "").Trim() ?? DBNull.Value}, {priority}, {(object)version ?? DBNull.Value}, {admin.UserName})");
            return Ok(new { ok = true });
        }

        [HttpPost("notes/{id:int}")]
        public async Task<IActionResult> UpdateNote(int id, [FromBody] NoteRequest req)
        {
            var admin = await RequireAdmin();
            if (admin == null) return Denied();
            var existing = await _db.Database.SqlQuery<NoteRow>($"SELECT Id, Type, Title, Body, Status, Priority, AppVersion, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt, CAST(NULL AS nvarchar(max)) AS PreviousBody FROM dbo.DevNotes WHERE Id = {id}").FirstOrDefaultAsync();
            if (existing == null) return NotFound(new { ok = false, error = "Note not found." });

            var type = NoteTypes.Contains(req.Type) ? req.Type : existing.Type;
            var status = NoteStatuses.Contains(req.Status) ? req.Status : existing.Status;
            var priority = NotePriorities.Contains(req.Priority) ? req.Priority : existing.Priority;
            var title = string.IsNullOrWhiteSpace(req.Title) ? existing.Title : req.Title.Trim();
            var body = req.Body == null ? existing.Body : req.Body.Trim();

            // Keep the previous text whenever the wording changes (status/priority-only changes don't count).
            if (title != existing.Title || (body ?? "") != (existing.Body ?? ""))
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.DevNoteRevisions (NoteId, Title, Body, SavedBy) VALUES ({id}, {existing.Title}, {(object)existing.Body ?? DBNull.Value}, {existing.UpdatedBy ?? existing.CreatedBy})");

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.DevNotes SET Type = {type}, Title = {title}, Body = {(object)body ?? DBNull.Value}, Status = {status}, Priority = {priority}, UpdatedBy = {admin.UserName}, UpdatedAt = SYSUTCDATETIME() WHERE Id = {id}");
            return Ok(new { ok = true });
        }

        [HttpDelete("notes/{id:int}")]
        public async Task<IActionResult> DeleteNote(int id)
        {
            if (await RequireAdmin() == null) return Denied();
            var n = await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dbo.DevNotes WHERE Id = {id}");
            return n == 0 ? NotFound(new { ok = false, error = "Note not found." }) : Ok(new { ok = true });
        }

        private async Task<int> Count(FormattableString sql) =>
            (await _db.Database.SqlQuery<CountRow>(sql).FirstOrDefaultAsync())?.Value ?? 0;

        private FileInfo FindInstaller()
        {
            var dir = new DirectoryInfo(Path.Combine(_env.ContentRootPath, "Installers"));
            if (!dir.Exists) return null;
            return dir.GetFiles().Where(f => f.Extension is ".exe" or ".msix" or ".msixbundle" or ".zip")
                .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        }

        private static string NewLicenceKey()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(12);
            var chars = bytes.Select(b => alphabet[b % alphabet.Length]).ToArray();
            return $"SC-{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
        }

        private static string TempPassword()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
            var b = RandomNumberGenerator.GetBytes(8);
            var word = new string(b.Select(x => letters[x % letters.Length]).ToArray());
            return $"Sc-{word}{RandomNumberGenerator.GetInt32(10, 99)}!";
        }
    }
}
