using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    // Test users: an admin flags an account; while signed in as it, the app reports each add / change /
    // remove and each document upload, and the admin page shows them as a live feed. Nothing is collected
    // for any other account, the app shows a banner on a flagged account, and the data is purged after
    // 30 days. Secrets (passwords, tokens, keys, PINs) are removed here as well as in the app.
    [ApiController]
    public class TestActivityController : ControllerBase
    {
        private const int RetentionDays = 30;
        private const long MaxFileBytes = 20 * 1024 * 1024;
        private static DateTime _lastPurge = DateTime.MinValue;
        private static readonly Regex Secret = new(@"pass(word|wd)?|secret|token|api_?key|licen[cs]e_?key|private_?key|credential|\botp\b|cookie|authori[sz]ation|^pin$|_pin$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;

        public TestActivityController(UserManager<ApplicationUser> users, ApplicationDbContext db, IWebHostEnvironment env)
        {
            _users = users; _db = db; _env = env;
        }

        public record IncomingEvent(DateTime At, string Area, string Kind, string Key, string Summary, JsonElement Data);
        public record IngestRequest(List<IncomingEvent> Events, string AppVersion);
        public record FlagRequest(bool On);

        private class EventRow { public long Id { get; set; } public DateTime At { get; set; } public string Area { get; set; } public string Kind { get; set; } public string ItemKey { get; set; } public string Summary { get; set; } public string Data { get; set; } public string AppVersion { get; set; } }
        private class FileRow { public int Id { get; set; } public DateTime At { get; set; } public string FileName { get; set; } public string Area { get; set; } public string Note { get; set; } public long SizeBytes { get; set; } public string StoredPath { get; set; } }
        private class CountRow { public string UserId { get; set; } public int Events { get; set; } public int Files { get; set; } public DateTime? Last { get; set; } }

        private string UploadsDir => Path.Combine(_env.ContentRootPath, "App_Data", "TestUploads");

        private async Task<ApplicationUser> RequireAdmin()
        {
            var u = await _users.GetUserAsync(User);
            return u != null && u.Administrator ? u : null;
        }
        private IActionResult Denied() => Unauthorized(new { ok = false, error = "Sign in as an administrator." });

        // ---------------- From the app (the test user, signed in) ----------------

        [HttpPost("api/account/test/events")]
        [RequestSizeLimit(2_000_000)]
        public async Task<IActionResult> Ingest([FromBody] IngestRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (!user.IsTestUser) return Ok(new { ok = true, active = false });   // the flag was removed: the app stops

            foreach (var e in (req?.Events ?? new()).Take(200))
            {
                if (string.IsNullOrWhiteSpace(e.Area) || string.IsNullOrWhiteSpace(e.Kind)) continue;
                var data = e.Data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? null : Redact(e.Data.GetRawText());
                if (data?.Length > 20000) data = data[..20000] + "…";
                var at = e.At == default || e.At > DateTime.UtcNow.AddMinutes(5) ? DateTime.UtcNow : e.At.ToUniversalTime();
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT dbo.TestEvents (UserId, At, Area, Kind, ItemKey, Summary, Data, AppVersion) VALUES ({user.Id}, {at}, {Cut(e.Area, 60)}, {Cut(e.Kind, 20)}, {Cut(e.Key, 200)}, {Cut(e.Summary ?? e.Kind, 400)}, {data}, {Cut(req.AppVersion, 40)})");
            }
            await PurgeOld();
            return Ok(new { ok = true, active = true });
        }

        [HttpPost("api/account/test/file")]
        [RequestSizeLimit(MaxFileBytes + 1024)]
        public async Task<IActionResult> UploadFile([FromQuery] string name, [FromQuery] string area, [FromQuery] string note)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (!user.IsTestUser) return Ok(new { ok = true, active = false });

            using var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            if (ms.Length == 0 || ms.Length > MaxFileBytes) return BadRequest(new { ok = false, error = "File is empty or over 20 MB." });

            var safe = new string((name ?? "file").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            if (safe.Length > 120) safe = safe[^120..];
            var dir = Path.Combine(UploadsDir, user.Id.Replace("/", "_").Replace("\\", "_"));
            Directory.CreateDirectory(dir);
            var stored = Path.Combine(dir, $"{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..28] + "-" + safe);
            await System.IO.File.WriteAllBytesAsync(stored, ms.ToArray());
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT dbo.TestFiles (UserId, FileName, Area, Note, SizeBytes, StoredPath) VALUES ({user.Id}, {safe}, {Cut(area, 60)}, {Cut(note, 300)}, {ms.Length}, {stored})");
            return Ok(new { ok = true, active = true });
        }

        // ---------------- Admin page ----------------

        [HttpPost("api/admin/test/flag/{id}")]
        public async Task<IActionResult> Flag(string id, [FromBody] FlagRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            var u = await _users.FindByIdAsync(id);
            if (u == null) return NotFound(new { ok = false, error = "User not found." });
            u.IsTestUser = req?.On == true;
            await _users.UpdateAsync(u);
            return Ok(new { ok = true, testUser = u.IsTestUser });
        }

        [HttpGet("api/admin/test/users")]
        public async Task<IActionResult> TestUsers()
        {
            if (await RequireAdmin() == null) return Denied();
            var flagged = await _db.Users.Where(u => u.IsTestUser).ToListAsync();
            var counts = await _db.Database.SqlQuery<CountRow>($@"
SELECT u.Id AS UserId,
       (SELECT COUNT(*) FROM dbo.TestEvents e WHERE e.UserId = u.Id) AS Events,
       (SELECT COUNT(*) FROM dbo.TestFiles f WHERE f.UserId = u.Id) AS Files,
       (SELECT MAX(e.At) FROM dbo.TestEvents e WHERE e.UserId = u.Id) AS [Last]
FROM dbo.AspNetUsers u WHERE u.IsTestUser = 1 OR u.Id IN (SELECT UserId FROM dbo.TestEvents)").ToListAsync();
            var all = flagged.Select(u => u.Id).Concat(counts.Select(c => c.UserId)).Distinct().ToList();
            var names = await _db.Users.Where(u => all.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new { u.UserName, u.IsTestUser });
            return Ok(new
            {
                ok = true,
                retentionDays = RetentionDays,
                users = all.Where(names.ContainsKey).Select(id =>
                {
                    var c = counts.FirstOrDefault(x => x.UserId == id);
                    return new { id, username = names[id].UserName, active = names[id].IsTestUser, events = c?.Events ?? 0, files = c?.Files ?? 0, last = c?.Last };
                }).OrderByDescending(x => x.active).ThenBy(x => x.username),
            });
        }

        [HttpGet("api/admin/test/events")]
        public async Task<IActionResult> Events([FromQuery] string userId, [FromQuery] long afterId = 0, [FromQuery] string area = null, [FromQuery] int limit = 200)
        {
            if (await RequireAdmin() == null) return Denied();
            limit = Math.Clamp(limit, 1, 500);
            var rows = await _db.Database.SqlQuery<EventRow>($@"
SELECT TOP ({limit}) Id, At, Area, Kind, ItemKey, Summary, Data, AppVersion
FROM dbo.TestEvents WHERE UserId = {userId} AND Id > {afterId} AND ({area} IS NULL OR {area} = '' OR Area = {area})
ORDER BY Id DESC").ToListAsync();
            var areas = await _db.Database.SqlQuery<string>($"SELECT DISTINCT Area AS [Value] FROM dbo.TestEvents WHERE UserId = {userId} ORDER BY Area").ToListAsync();
            return Ok(new { ok = true, areas, events = rows.Select(r => new { r.Id, r.At, r.Area, r.Kind, key = r.ItemKey, r.Summary, r.Data, r.AppVersion }) });
        }

        [HttpGet("api/admin/test/files")]
        public async Task<IActionResult> Files([FromQuery] string userId)
        {
            if (await RequireAdmin() == null) return Denied();
            var rows = await _db.Database.SqlQuery<FileRow>($"SELECT TOP 200 Id, At, FileName, Area, Note, SizeBytes, StoredPath FROM dbo.TestFiles WHERE UserId = {userId} ORDER BY Id DESC").ToListAsync();
            return Ok(new { ok = true, files = rows.Select(r => new { r.Id, r.At, name = r.FileName, r.Area, r.Note, r.SizeBytes, exists = System.IO.File.Exists(r.StoredPath) }) });
        }

        [HttpGet("api/admin/test/file/{id:int}")]
        public async Task<IActionResult> DownloadFile(int id)
        {
            if (await RequireAdmin() == null) return Denied();
            var r = await _db.Database.SqlQuery<FileRow>($"SELECT Id, At, FileName, Area, Note, SizeBytes, StoredPath FROM dbo.TestFiles WHERE Id = {id}").FirstOrDefaultAsync();
            if (r == null || !System.IO.File.Exists(r.StoredPath)) return NotFound(new { ok = false, error = "File not found (it may have been purged)." });
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return PhysicalFile(r.StoredPath, "application/octet-stream", r.FileName);   // always a download, never rendered in the admin origin
        }

        [HttpPost("api/admin/test/clear/{userId}")]
        public async Task<IActionResult> Clear(string userId)
        {
            if (await RequireAdmin() == null) return Denied();
            var files = await _db.Database.SqlQuery<FileRow>($"SELECT Id, At, FileName, Area, Note, SizeBytes, StoredPath FROM dbo.TestFiles WHERE UserId = {userId}").ToListAsync();
            foreach (var f in files) try { System.IO.File.Delete(f.StoredPath); } catch { }
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.TestFiles WHERE UserId = {userId}");
            var n = await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.TestEvents WHERE UserId = {userId}");
            return Ok(new { ok = true, deleted = n });
        }

        // ---------------- helpers ----------------

        private async Task PurgeOld()
        {
            if (DateTime.UtcNow - _lastPurge < TimeSpan.FromHours(1)) return;
            _lastPurge = DateTime.UtcNow;
            var cutoff = DateTime.UtcNow.AddDays(-RetentionDays);
            var old = await _db.Database.SqlQuery<FileRow>($"SELECT Id, At, FileName, Area, Note, SizeBytes, StoredPath FROM dbo.TestFiles WHERE At < {cutoff}").ToListAsync();
            foreach (var f in old) try { System.IO.File.Delete(f.StoredPath); } catch { }
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.TestFiles WHERE At < {cutoff}");
            await _db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.TestEvents WHERE ReceivedAt < {cutoff}");
        }

        private static string Cut(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);

        // Second line of defence: anything whose property name looks like a secret is replaced.
        private static string Redact(string json)
        {
            try
            {
                var node = JsonNode.Parse(json);
                RedactNode(node);
                return node?.ToJsonString();
            }
            catch { return null; }
        }
        private static void RedactNode(JsonNode n)
        {
            if (n is JsonObject o)
            {
                foreach (var k in o.Select(p => p.Key).ToList())
                {
                    if (Secret.IsMatch(k)) o[k] = "[hidden]";
                    else RedactNode(o[k]);
                }
            }
            else if (n is JsonArray a) foreach (var item in a) RedactNode(item);
        }
    }
}
