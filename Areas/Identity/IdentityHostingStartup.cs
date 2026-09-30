using System;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: HostingStartup(typeof(SmartCubeMobileV2026.Areas.Identity.IdentityHostingStartup))]
namespace SmartCubeMobileV2026.Areas.Identity
{
    public class IdentityHostingStartup : IHostingStartup
    {
        public void Configure(IWebHostBuilder builder)
        {
            builder.ConfigureServices((context, services) =>
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(
                        context.Configuration.GetConnectionString("DefaultConnection")));

                // Keys for cookies, reset links and the two-factor secret encryption: stored in the
                // database so they survive restarts, each key encrypted with this machine's DPAPI key
                // (a copy of the database on its own can't decrypt anything).
                services.AddDataProtection()
                    .SetApplicationName("SmartCubeMobileV2026")
                    .PersistKeysToDbContext<ApplicationDbContext>()
                    .ProtectKeysWithDpapi(protectToLocalMachine: true);

                services.AddDefaultIdentity<ApplicationUser>(options =>
                    {
                        options.SignIn.RequireConfirmedAccount = false;
                    })
                    .AddEntityFrameworkStores<ApplicationDbContext>();

                // Replace the default user store with one that encrypts two-factor secrets and recovery codes.
                services.AddScoped<IUserStore<ApplicationUser>, ProtectedUserStore>();

            });
        }
    }
}