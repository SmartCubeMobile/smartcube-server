using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Services
{
    // Pings the public website and this server every few minutes and keeps the results in dbo.SiteChecks
    // for the admin page's "Website monitoring" cards. Targets come from appsettings Monitoring:Targets
    // (name + url); the interval from Monitoring:IntervalMinutes (default 5). 30 days of history are kept.
    public class SiteMonitor : BackgroundService
    {
        private static readonly HttpClient _http = new(new HttpClientHandler { AllowAutoRedirect = true }) { Timeout = TimeSpan.FromSeconds(20) };
        private readonly IServiceScopeFactory _scopes;
        private readonly IConfiguration _config;
        private readonly ILogger<SiteMonitor> _log;

        public SiteMonitor(IServiceScopeFactory scopes, IConfiguration config, ILogger<SiteMonitor> log)
        {
            _scopes = scopes; _config = config; _log = log;
        }

        public record Target(string Name, string Url);

        public List<Target> Targets()
        {
            var list = _config.GetSection("Monitoring:Targets").GetChildren()
                .Select(s => new Target(s["Name"], s["Url"]))
                .Where(t => !string.IsNullOrWhiteSpace(t.Name) && !string.IsNullOrWhiteSpace(t.Url)).ToList();
            if (list.Count == 0)
                list = new List<Target>
                {
                    new("Website", "https://www.smartcubemobile.com/"),
                    new("API server", "https://api.smartcubemobile.com/api/account/download/status"),
                };
            return list;
        }

        protected override async Task ExecuteAsync(CancellationToken stop)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stop);   // let the site finish starting
            while (!stop.IsCancellationRequested)
            {
                try { await CheckAll(stop); }
                catch (Exception ex) { _log.LogWarning(ex, "Site monitor pass failed"); }
                var minutes = int.TryParse(_config["Monitoring:IntervalMinutes"], out var m) && m > 0 ? m : 5;
                await Task.Delay(TimeSpan.FromMinutes(minutes), stop);
            }
        }

        public async Task CheckAll(CancellationToken stop)
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var t in Targets())
            {
                var sw = Stopwatch.StartNew();
                bool ok; int? status = null; string error = null;
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, t.Url);
                    req.Headers.UserAgent.ParseAdd("SmartCubeMobile-Monitor/1.0");
                    using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, stop);
                    status = (int)res.StatusCode;
                    ok = res.IsSuccessStatusCode;
                    if (!ok) error = $"HTTP {status}";
                }
                catch (Exception ex) { ok = false; error = ex.GetBaseException().Message; }
                sw.Stop();
                if (error?.Length > 400) error = error[..400];
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT dbo.SiteChecks (Target, Ok, StatusCode, Ms, Error) VALUES ({t.Name}, {ok}, {status}, {(int)sw.ElapsedMilliseconds}, {error})", stop);
            }
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE dbo.SiteChecks WHERE CheckedAt < {DateTime.UtcNow.AddDays(-30)}", stop);
        }
    }
}
