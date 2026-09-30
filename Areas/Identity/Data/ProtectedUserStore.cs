using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Areas.Identity.Data
{
    // Identity stores the authenticator (TOTP) secret and the recovery codes as plain text in
    // AspNetUserTokens by default. This store encrypts those two values with ASP.NET Data Protection
    // before they reach the database and decrypts them on the way back. Everything else is unchanged.
    // Stored form: "p1:" + protected payload. Older plain values are still read, and are converted
    // by ProtectExistingAsync at startup.
    public class ProtectedUserStore : UserOnlyStore<ApplicationUser, ApplicationDbContext>
    {
        public const string Purpose = "SmartCube.UserTokens.v1";
        private const string Prefix = "p1:";
        private const string InternalProvider = "[AspNetUserStore]";
        private readonly IDataProtector _protector;

        public ProtectedUserStore(ApplicationDbContext context, IDataProtectionProvider dataProtection, IdentityErrorDescriber describer = null)
            : base(context, describer)
        {
            _protector = dataProtection.CreateProtector(Purpose);
        }

        private static bool IsSensitive(string loginProvider, string name) =>
            loginProvider == InternalProvider && (name == "AuthenticatorKey" || name == "RecoveryCodes");

        public override Task SetTokenAsync(ApplicationUser user, string loginProvider, string name, string value, CancellationToken cancellationToken)
        {
            if (IsSensitive(loginProvider, name) && !string.IsNullOrEmpty(value))
                value = Prefix + _protector.Protect(value);
            return base.SetTokenAsync(user, loginProvider, name, value, cancellationToken);
        }

        public override async Task<string> GetTokenAsync(ApplicationUser user, string loginProvider, string name, CancellationToken cancellationToken)
        {
            var value = await base.GetTokenAsync(user, loginProvider, name, cancellationToken);
            if (IsSensitive(loginProvider, name) && value != null && value.StartsWith(Prefix, StringComparison.Ordinal))
                return _protector.Unprotect(value[Prefix.Length..]);
            return value;
        }

        private class TokenRow { public string UserId { get; set; } public string LoginProvider { get; set; } public string Name { get; set; } public string Value { get; set; } }

        // One-off conversion of secrets saved before this store existed. Safe to run on every start.
        public static async Task<int> ProtectExistingAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector(Purpose);

            var rows = await db.Database.SqlQuery<TokenRow>($@"
SELECT UserId, LoginProvider, Name, Value FROM dbo.AspNetUserTokens
WHERE LoginProvider = {InternalProvider} AND Name IN ('AuthenticatorKey', 'RecoveryCodes')
  AND Value IS NOT NULL AND Value <> '' AND Value NOT LIKE 'p1:%'").ToListAsync();

            foreach (var r in rows)
            {
                var protectedValue = Prefix + protector.Protect(r.Value);
                await db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE dbo.AspNetUserTokens SET Value = {protectedValue} WHERE UserId = {r.UserId} AND LoginProvider = {r.LoginProvider} AND Name = {r.Name} AND Value = {r.Value}");
            }
            return rows.Count;
        }
    }
}
