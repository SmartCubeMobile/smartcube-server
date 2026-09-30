using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SmartCubeMobileV2026
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        //public void ConfigureServices(IServiceCollection services)
        //{
        //    //services.AddDbContext<ApplicationDbContext>(options =>
        //    //    options.UseSqlServer(
        //    //        Configuration.GetConnectionString("DefaultConnection")));            
        //    //services.AddDefaultIdentity<IdentityUser>() //options => options.SignIn.RequireConfirmedAccount = true)
        //    //    .AddEntityFrameworkStores<ApplicationDbContext>();

        //    services.AddDatabaseDeveloperPageExceptionFilter();
        //    services.AddControllersWithViews();
        //    services.AddRazorPages();
        //}

        //public void ConfigureServices(IServiceCollection services)
        //{
        //    services.AddDbContext<ApplicationDbContext>(options =>
        //        options.UseSqlServer(
        //            Configuration.GetConnectionString("DefaultConnection")));

        //    services.AddDefaultIdentity<ApplicationUser>(options =>
        //    {
        //        options.SignIn.RequireConfirmedAccount = false;
        //    })
        //    .AddEntityFrameworkStores<ApplicationDbContext>();

        //    services.AddControllersWithViews();
        //    services.AddRazorPages();
        //}

        public void ConfigureServices(IServiceCollection services)
        {
            SmartScan.Configure(Configuration);
            Controllers.ChatTokens.Configure(Configuration);

            services.AddDatabaseDeveloperPageExceptionFilter();

            services.AddControllersWithViews().AddJsonOptions(o =>
            {
                o.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                o.JsonSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
            });

            services.AddRazorPages();

            services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, Services.SmtpEmailSender>();

            // Live support chat. Long-polling timeout is kept under the Netlify proxy's limit so the
            // live site (which can't proxy WebSockets) still works; the api host uses WebSockets.
            services.AddSignalR(o => { o.EnableDetailedErrors = false; o.MaximumReceiveMessageSize = 64 * 1024; })
                .AddJsonProtocol(o =>
                {
                    o.PayloadSerializerOptions.Converters.Add(new UtcDateTimeConverter());
                    o.PayloadSerializerOptions.Converters.Add(new UtcNullableDateTimeConverter());
                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            // Encrypt any two-factor secrets / recovery codes saved before ProtectedUserStore existed.
            try
            {
                var converted = SmartCubeMobileV2026.Areas.Identity.Data.ProtectedUserStore
                    .ProtectExistingAsync(app.ApplicationServices).GetAwaiter().GetResult();
                if (converted > 0)
                    app.ApplicationServices.GetRequiredService<ILogger<Startup>>()
                        .LogInformation("Encrypted {Count} existing two-factor secrets", converted);
            }
            catch (Exception ex)
            {
                app.ApplicationServices.GetRequiredService<ILogger<Startup>>()
                    .LogError(ex, "Could not encrypt existing two-factor secrets");
            }

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    var path = ctx.Context.Request.Path;
                    if (path.StartsWithSegments("/admin") || path.StartsWithSegments("/site"))
                        ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
                }
            });

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseWebSockets();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapHub<Hubs.SupportChatHub>("/api/support/hub", o =>
                {
                    o.LongPolling.PollTimeout = TimeSpan.FromSeconds(20);
                });
                endpoints.MapGet("/admin", context =>
                {
                    context.Response.Redirect("/admin/index.html");
                    return Task.CompletedTask;
                });
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapRazorPages();
            });

            // So I can get to my Handlers
            app.MapWhen(context => context.Request.Path.ToString().EndsWith(".ashx"),
                appBuilder => {
                    appBuilder.UseRedirectionHanlderMiddleware();
                });
        }
    }

    // All DateTimes in the DB are stored as UTC but come back with Kind=Unspecified; without this the
    // JSON has no 'Z' and browsers in other timezones treat the value as local time.
    public class UtcDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            => reader.GetDateTime();

        public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime value, System.Text.Json.JsonSerializerOptions options)
        {
            if (value == default) { writer.WriteNullValue(); return; }
            var utc = value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
            writer.WriteStringValue(utc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"));
        }
    }

    public class UtcNullableDateTimeConverter : System.Text.Json.Serialization.JsonConverter<DateTime?>
    {
        private static readonly UtcDateTimeConverter Inner = new();

        public override DateTime? Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            => reader.TokenType == System.Text.Json.JsonTokenType.Null ? null : reader.GetDateTime();

        public override void Write(System.Text.Json.Utf8JsonWriter writer, DateTime? value, System.Text.Json.JsonSerializerOptions options)
        {
            if (value.HasValue) Inner.Write(writer, value.Value, options); else writer.WriteNullValue();
        }
    }

    public class RedirectionHandlerMiddleware
    {
        private RequestDelegate _next;

        public RedirectionHandlerMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            //await context.Response.WriteAsync("<p>Process files with .ashx extension</p>");

            //Debugger.Break();

            string which = context.Request.Path.ToString();

            // Only Smart Scan is used by the current app. The old SmartDBServer-era handlers (direct table,
            // stored-procedure and proxy access guarded only by a Referer header) are off unless
            // appsettings "LegacyHandlers:Enabled" is true. Switched off 2026-09-28 once the server became
            // reachable from the internet.
            if (!string.Equals(which, "/Handlers/SmartScan.ashx", StringComparison.OrdinalIgnoreCase))
            {
                var config = context.RequestServices.GetService<IConfiguration>();
                if (!string.Equals(config?["LegacyHandlers:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
                {
                    context.Response.StatusCode = 404;
                    return;
                }
            }

            switch (which)
            {
                case "/Handlers/Connect.ashx":
                    await Connect.Invoke(context);
                    break;
                case "/Handlers/DBServer.ashx":
                    await DBServer.Invoke(context);
                    break;
                case "/Handlers/Download_PDF.ashx":
                    await Download_PDF.Invoke(context);
                    break;
                case "/Handlers/InsertCOMMON.ashx":
                    await InsertCOMMON.Invoke(context);
                    break;
                case "/Handlers/Listener.ashx":
                    await Listener.Invoke(context);
                    break;
                case "/Handlers/LoadTable.ashx":
                    await LoadTable.Invoke(context);
                    break;
                case "/Handlers/LookupLOGO.ashx":
                    await LookupLOGO.Invoke(context);
                    break;
                case "/Handlers/StoredProcedure.ashx":
                    await StoredProcedure.Invoke(context);
                    break;
                case "/Handlers/Webproxy.ashx":
                    await Webproxy.Invoke(context);
                    break;
                case "/Handlers/SmartScan.ashx":
                    await SmartScan.Invoke(context);
                    break;
                default:
                    break;
            }
            return;
        }
    }

    public static class MiddlewareExtension
    {
        public static IApplicationBuilder UseRedirectionHanlderMiddleware
                      (this IApplicationBuilder applicationBuilder)
        {
            return applicationBuilder.UseMiddleware<RedirectionHandlerMiddleware>();
        }
    }
}
