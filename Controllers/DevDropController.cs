using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartCubeMobileV2026.Areas.Identity.Data;

namespace SmartCubeMobileV2026.Controllers
{
    // "Drop zone" on the admin page: Dad drags his project folder onto the page and it becomes a commit
    // on the shared GitHub repo, with no git on his PC. Everything goes through GitHub's REST API with a
    // token from appsettings (GitHub:Token), so nothing is cloned or written on this server.
    //
    //   1. preview : browser sends every file's path + git blob SHA; we diff against the branch tip
    //   2. blobs   : browser uploads only the changed files (batched); we create GitHub blobs
    //   3. commit  : new tree on top of the tip we previewed, commit, move the branch
    [ApiController]
    [Route("api/admin/drop")]
    public class DevDropController : ControllerBase
    {
        private static readonly HttpClient _gh = new() { Timeout = TimeSpan.FromSeconds(120) };
        private readonly UserManager<ApplicationUser> _users;
        private readonly IConfiguration _config;

        public DevDropController(UserManager<ApplicationUser> users, IConfiguration config)
        {
            _users = users;
            _config = config;
        }

        public record FileStamp(string Path, string Sha, long Size);
        public record PreviewRequest(List<FileStamp> Files);
        public record BlobUpload(string Path, string ContentBase64);
        public record BlobsRequest(List<BlobUpload> Files);
        public record Change(string Path, string Sha);   // Sha null = delete
        public record CommitRequest(string BaseSha, string Message, List<Change> Changes);

        // Folders/files that never belong in the repo (mirrors the .gitignore files) and the project file
        // that proves the right folder was dropped.
        private static readonly string[] IgnoredDirs = { "bin", "obj", ".vs", ".git", "node_modules", "packages", "App_Data", "Installers", "logs", "Users/Data" };
        private static readonly string[] IgnoredFiles = { "appsettings.json", "app_offline.htm" };
        private static readonly string[] IgnoredExtensions = { ".user", ".suo", ".bak", ".mdf", ".ldf", ".db", ".xlsx" };

        private (string Repo, string ProjectFile) Target(string part) => part?.ToLowerInvariant() switch
        {
            "app" => (_config["GitHub:AppRepo"] ?? "smartcube-app", "SmartCubeMobile.csproj"),
            "server" => (_config["GitHub:ServerRepo"] ?? "smartcube-server", "SmartCubeMobileV2026.csproj"),
            _ => (null, null),
        };
        private string Owner => _config["GitHub:Owner"] ?? "SmartCubeMobile";
        private string Branch => _config["GitHub:Branch"] ?? "main";

        private async Task<ApplicationUser> RequireAdmin()
        {
            var u = await _users.GetUserAsync(User);
            return u != null && u.Administrator ? u : null;
        }
        private IActionResult Denied() => Unauthorized(new { ok = false, error = "Sign in as an administrator." });

        private IActionResult NotConfigured() =>
            StatusCode(503, new { ok = false, error = "The drop zone isn't set up: add a GitHub token (GitHub:Token) to the server's appsettings.json." });

        public static bool IsIgnored(string path)
        {
            var parts = path.Split('/');
            if (parts.Length > 1 && IgnoredDirs.Any(d => path.StartsWith(d + "/", StringComparison.OrdinalIgnoreCase) || path.Contains("/" + d + "/", StringComparison.OrdinalIgnoreCase)))
                return true;
            var name = parts[^1];
            if (IgnoredFiles.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
            if (IgnoredExtensions.Any(e => name.EndsWith(e, StringComparison.OrdinalIgnoreCase))) return true;
            return false;
        }

        private static bool SafePath(string p) =>
            !string.IsNullOrWhiteSpace(p) && !p.StartsWith("/") && !p.Contains("\\") && !p.Split('/').Any(s => s is "" or "." or "..");

        [HttpPost("{part}/preview")]
        public async Task<IActionResult> Preview(string part, [FromBody] PreviewRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            var (repo, projectFile) = Target(part);
            if (repo == null) return BadRequest(new { ok = false, error = "Unknown target." });
            if (string.IsNullOrWhiteSpace(_config["GitHub:Token"])) return NotConfigured();
            var files = (req?.Files ?? new()).Where(f => SafePath(f.Path)).ToList();
            if (files.Count == 0) return BadRequest(new { ok = false, error = "No files received." });

            if (!files.Any(f => string.Equals(f.Path, projectFile, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { ok = false, error = $"That doesn't look like the {part} project folder: {projectFile} isn't at its top level. Drop the folder that contains {projectFile}." });

            var dropped = files.Where(f => !IsIgnored(f.Path)).ToDictionary(f => f.Path, f => f, StringComparer.Ordinal);
            var skipped = files.Count - dropped.Count;

            var tip = await BranchTip(repo);
            if (tip == null) return StatusCode(502, new { ok = false, error = "Couldn't read the GitHub repository. Check the token and repository name." });
            var current = await TreeFiles(repo, tip.Value.TreeSha);

            var added = dropped.Keys.Where(p => !current.ContainsKey(p)).OrderBy(p => p).ToList();
            var modified = dropped.Where(kv => current.TryGetValue(kv.Key, out var sha) && !string.Equals(sha, kv.Value.Sha, StringComparison.OrdinalIgnoreCase)).Select(kv => kv.Key).OrderBy(p => p).ToList();
            var deleted = current.Keys.Where(p => !dropped.ContainsKey(p) && !IsIgnored(p)).OrderBy(p => p).ToList();
            var unchanged = dropped.Count - added.Count - modified.Count;

            var warnings = new List<string>();
            if (deleted.Count > Math.Max(10, current.Count / 10))
                warnings.Add($"This would delete {deleted.Count} of the {current.Count} files in the repository. Check you dropped the whole project folder, not a part of it.");
            if (dropped.Keys.Any(p => p.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)) == false && part.Equals("app", StringComparison.OrdinalIgnoreCase))
                warnings.Add("No .sln file was found in the folder.");

            return Ok(new
            {
                ok = true,
                repo = $"{Owner}/{repo}",
                baseSha = tip.Value.CommitSha,
                baseMessage = tip.Value.Message,
                added, modified, deleted, unchanged, skipped,
                uploadBytes = added.Concat(modified).Sum(p => dropped[p].Size),
                warnings,
            });
        }

        // The browser sends ~15 MB of files per request (20 MB as base64) to stay under IIS's 30 MB limit.
        [HttpPost("{part}/blobs")]
        [RequestSizeLimit(30_000_000)]
        public async Task<IActionResult> Blobs(string part, [FromBody] BlobsRequest req)
        {
            if (await RequireAdmin() == null) return Denied();
            var (repo, _) = Target(part);
            if (repo == null) return BadRequest(new { ok = false, error = "Unknown target." });
            if (string.IsNullOrWhiteSpace(_config["GitHub:Token"])) return NotConfigured();

            var results = new List<object>();
            foreach (var f in req?.Files ?? new())
            {
                if (!SafePath(f.Path) || IsIgnored(f.Path)) continue;
                var bytes = Convert.FromBase64String(f.ContentBase64 ?? "");
                var expected = GitBlobSha(bytes);
                using var res = await Send(HttpMethod.Post, $"repos/{Owner}/{repo}/git/blobs", new { content = f.ContentBase64, encoding = "base64" });
                if (!res.IsSuccessStatusCode)
                    return StatusCode(502, new { ok = false, error = $"GitHub refused {f.Path} ({(int)res.StatusCode})." });
                var sha = (await ReadJson(res)).GetProperty("sha").GetString();
                if (!string.Equals(sha, expected, StringComparison.OrdinalIgnoreCase))
                    return StatusCode(502, new { ok = false, error = $"{f.Path} was corrupted in transit. Try again." });
                results.Add(new { path = f.Path, sha });
            }
            return Ok(new { ok = true, files = results });
        }

        [HttpPost("{part}/commit")]
        public async Task<IActionResult> Commit(string part, [FromBody] CommitRequest req)
        {
            var user = await RequireAdmin();
            if (user == null) return Denied();
            var (repo, _) = Target(part);
            if (repo == null) return BadRequest(new { ok = false, error = "Unknown target." });
            if (string.IsNullOrWhiteSpace(_config["GitHub:Token"])) return NotConfigured();
            var changes = (req?.Changes ?? new()).Where(c => SafePath(c.Path) && !IsIgnored(c.Path)).ToList();
            if (changes.Count == 0) return BadRequest(new { ok = false, error = "Nothing to commit." });

            var tip = await BranchTip(repo);
            if (tip == null) return StatusCode(502, new { ok = false, error = "Couldn't read the GitHub repository." });
            if (!string.Equals(tip.Value.CommitSha, req.BaseSha, StringComparison.OrdinalIgnoreCase))
                return Conflict(new { ok = false, error = "The repository changed since the preview. Drop the folder again to get a fresh comparison." });

            var tree = changes.Select(c => c.Sha == null
                ? (object)new { path = c.Path, mode = "100644", type = "blob", sha = (string)null }
                : new { path = c.Path, mode = "100644", type = "blob", sha = c.Sha }).ToList();
            using (var t = await Send(HttpMethod.Post, $"repos/{Owner}/{repo}/git/trees", new { base_tree = tip.Value.TreeSha, tree }))
            {
                if (!t.IsSuccessStatusCode) return StatusCode(502, new { ok = false, error = $"GitHub couldn't build the tree ({(int)t.StatusCode}): {await t.Content.ReadAsStringAsync()}" });
                var treeSha = (await ReadJson(t)).GetProperty("sha").GetString();

                var who = new { name = user.UserName, email = user.Email ?? $"{user.UserName}@smartcubemobile.com" };
                var message = string.IsNullOrWhiteSpace(req.Message) ? $"Drop zone update from {who.name}" : req.Message.Trim();
                using var c = await Send(HttpMethod.Post, $"repos/{Owner}/{repo}/git/commits", new { message, tree = treeSha, parents = new[] { tip.Value.CommitSha }, author = who, committer = who });
                if (!c.IsSuccessStatusCode) return StatusCode(502, new { ok = false, error = $"GitHub couldn't create the commit ({(int)c.StatusCode})." });
                var commitSha = (await ReadJson(c)).GetProperty("sha").GetString();

                using var r = await Send(HttpMethod.Patch, $"repos/{Owner}/{repo}/git/refs/heads/{Branch}", new { sha = commitSha, force = false });
                if (!r.IsSuccessStatusCode) return StatusCode(502, new { ok = false, error = $"GitHub wouldn't move the branch ({(int)r.StatusCode}). Drop the folder again." });

                return Ok(new { ok = true, sha = commitSha, url = $"https://github.com/{Owner}/{repo}/commit/{commitSha}", message });
            }
        }

        // The current shared code as a zip (GitHub's zipball), so Dad can start from the latest without git.
        [HttpGet("{part}/zip")]
        public async Task<IActionResult> Zip(string part)
        {
            if (await RequireAdmin() == null) return Denied();
            var (repo, _) = Target(part);
            if (repo == null) return BadRequest(new { ok = false, error = "Unknown target." });
            if (string.IsNullOrWhiteSpace(_config["GitHub:Token"])) return NotConfigured();
            var res = await Send(HttpMethod.Get, $"repos/{Owner}/{repo}/zipball/{Branch}", null);
            if (!res.IsSuccessStatusCode) return StatusCode(502, new { ok = false, error = $"GitHub wouldn't give the zip ({(int)res.StatusCode})." });
            var stream = await res.Content.ReadAsStreamAsync();
            return File(stream, "application/zip", $"{repo}-{DateTime.Now:yyyyMMdd-HHmm}.zip");
        }

        // ---------------- GitHub helpers ----------------

        private async Task<(string CommitSha, string TreeSha, string Message)?> BranchTip(string repo)
        {
            using var res = await Send(HttpMethod.Get, $"repos/{Owner}/{repo}/branches/{Branch}", null);
            if (!res.IsSuccessStatusCode) return null;
            var j = await ReadJson(res);
            var commit = j.GetProperty("commit");
            return (commit.GetProperty("sha").GetString(),
                    commit.GetProperty("commit").GetProperty("tree").GetProperty("sha").GetString(),
                    commit.GetProperty("commit").GetProperty("message").GetString());
        }

        private async Task<Dictionary<string, string>> TreeFiles(string repo, string treeSha)
        {
            using var res = await Send(HttpMethod.Get, $"repos/{Owner}/{repo}/git/trees/{treeSha}?recursive=1", null);
            var j = await ReadJson(res);
            var files = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var e in j.GetProperty("tree").EnumerateArray())
                if (e.GetProperty("type").GetString() == "blob")
                    files[e.GetProperty("path").GetString()] = e.GetProperty("sha").GetString();
            return files;
        }

        private Task<HttpResponseMessage> Send(HttpMethod method, string path, object body)
        {
            var req = new HttpRequestMessage(method, "https://api.github.com/" + path);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _config["GitHub:Token"]);
            req.Headers.UserAgent.ParseAdd("SmartCubeMobile-DropZone");
            req.Headers.Accept.ParseAdd("application/vnd.github+json");
            req.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            if (body != null)
                req.Content = new StringContent(JsonSerializer.Serialize(body, new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.Never }), Encoding.UTF8, "application/json");
            return _gh.SendAsync(req);
        }

        private static async Task<JsonElement> ReadJson(HttpResponseMessage res) =>
            JsonDocument.Parse(await res.Content.ReadAsStringAsync()).RootElement.Clone();

        // git's blob id: sha1("blob <length>\0" + bytes)
        public static string GitBlobSha(byte[] content)
        {
            var header = Encoding.ASCII.GetBytes($"blob {content.Length}\0");
            var all = new byte[header.Length + content.Length];
            header.CopyTo(all, 0); content.CopyTo(all, header.Length);
            return Convert.ToHexString(SHA1.HashData(all)).ToLowerInvariant();
        }
    }
}
