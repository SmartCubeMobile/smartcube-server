using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobile;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    [ApiController]
    [Route("api/account")]
    public class AccountApiController : ControllerBase
    {
        private const string FullNameClaim = "full_name";
        private const string PlanClaim = "plan";

        private readonly UserManager<ApplicationUser> _users;
        private readonly SignInManager<ApplicationUser> _signIn;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly Microsoft.AspNetCore.DataProtection.IDataProtector _deviceKeyProtector;
        private readonly IConfiguration _config;
        private static readonly HttpClient _trueLayerHttp = new() { Timeout = TimeSpan.FromSeconds(30) };

        public AccountApiController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
            ApplicationDbContext db, IWebHostEnvironment env, Microsoft.AspNetCore.DataProtection.IDataProtectionProvider dataProtection,
            IConfiguration config)
        {
            _deviceKeyProtector = dataProtection.CreateProtector("SmartCube.DeviceKeys.v1");
            _users = users;
            _signIn = signIn;
            _db = db;
            _env = env;
            _config = config;
        }

        // ---- TrueLayer token exchange ----
        // The TrueLayer client secret lives only here (appsettings TrueLayer:ClientSecret). The app sends
        // the one-time auth code or its refresh token; the bank tokens go straight back to the app and are
        // never stored on the server.
        public record TrueLayerTokenRequest(string GrantType, string Code, string RedirectUri, string RefreshToken);

        [HttpPost("truelayer/token")]
        public async Task<IActionResult> TrueLayerToken([FromBody] TrueLayerTokenRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            var clientId = _config["TrueLayer:ClientId"];
            var clientSecret = _config["TrueLayer:ClientSecret"];
            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return StatusCode(503, new { ok = false, error = "Bank connections aren't set up on this server." });

            var form = new Dictionary<string, string> { ["client_id"] = clientId, ["client_secret"] = clientSecret };
            switch (req?.GrantType)
            {
                case "authorization_code":
                    if (string.IsNullOrWhiteSpace(req.Code)) return BadRequest(new { ok = false, error = "Missing auth code." });
                    form["grant_type"] = "authorization_code";
                    form["code"] = req.Code;
                    form["redirect_uri"] = string.IsNullOrWhiteSpace(req.RedirectUri) ? "http://localhost:3000/callback" : req.RedirectUri;
                    break;
                case "refresh_token":
                    if (string.IsNullOrWhiteSpace(req.RefreshToken)) return BadRequest(new { ok = false, error = "Missing refresh token." });
                    form["grant_type"] = "refresh_token";
                    form["refresh_token"] = req.RefreshToken;
                    break;
                default:
                    return BadRequest(new { ok = false, error = "Unknown grant type." });
            }

            try
            {
                using var response = await _trueLayerHttp.PostAsync("https://auth.truelayer.com/connect/token", new FormUrlEncodedContent(form));
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    return StatusCode(502, new { ok = false, error = $"TrueLayer refused ({(int)response.StatusCode}): {body}" });
                var json = System.Text.Json.JsonDocument.Parse(body).RootElement;
                return Ok(new
                {
                    ok = true,
                    accessToken = json.TryGetProperty("access_token", out var a) ? a.GetString() : null,
                    refreshToken = json.TryGetProperty("refresh_token", out var r) ? r.GetString() : null,
                    expiresIn = json.TryGetProperty("expires_in", out var e) && e.TryGetInt32(out var secs) ? secs : (int?)null,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(502, new { ok = false, error = "Could not reach TrueLayer: " + ex.Message });
            }
        }

        public record RegisterRequest(string Username, string FullName, string Email, string Password);
        public record LoginRequest(string Login, string Password, bool Remember, string Code);
        public record TwoFactorCodeRequest(string Code);
        public record SubscribeRequest(string Plan);
        public record DeviceRegisterRequest(string DeviceName, string Pin);
        public record DeviceLoginRequest(string Token, string Pin);
        public record DevicePinRequest(string Token, string Pin);

        // ---- Remembered-device PIN ----
        // The PIN is checked here, not in the app: a stolen device token is useless without it, and
        // MaxPinFailures wrong tries forget the device so the full password is needed again.
        private const int MaxPinFailures = 5;
        private const int PinIterations = 200_000;
        private class DeviceRow { public string UserId { get; set; } public string PinHash { get; set; } public int PinFailures { get; set; } public string DeviceKey { get; set; } }

        // Per-device key share for the app's local "this PC" key slot. The app combines it with a secret
        // only that Windows account can read; the server hands it out only after a correct PIN (or to a
        // password-signed-in owner). It is useless on its own: the server never sees the user's data key.
        private string NewDeviceKey() => _deviceKeyProtector.Protect(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        private string OpenDeviceKey(string stored)
        {
            try { return string.IsNullOrEmpty(stored) ? null : _deviceKeyProtector.Unprotect(stored); }
            catch { return null; }
        }

        // Returns the device key for a device (creating one if it has none yet).
        private async Task<string> EnsureDeviceKey(string tokenHash, string stored)
        {
            var key = OpenDeviceKey(stored);
            if (key != null) return key;
            var fresh = NewDeviceKey();
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.DeviceTokens SET DeviceKey = {fresh} WHERE TokenHash = {tokenHash}");
            return OpenDeviceKey(fresh);
        }

        // The signed-in owner (password session) fetches their remembered device's key to create the
        // app's device slot, e.g. on the first password sign-in after this feature arrived.
        [HttpPost("device/key")]
        public async Task<IActionResult> DeviceKey([FromBody] DevicePinRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (string.IsNullOrWhiteSpace(req?.Token)) return BadRequest(new { ok = false, error = "This device isn't remembered." });
            var hash = Sha256(req.Token.Trim());
            var row = await _db.Database.SqlQuery<DeviceRow>($"SELECT UserId, PinHash, PinFailures, DeviceKey FROM dbo.DeviceTokens WHERE TokenHash = {hash} AND UserId = {user.Id} AND Revoked = 0").FirstOrDefaultAsync();
            if (row == null) return BadRequest(new { ok = false, error = "This device is no longer remembered." });
            if (row.PinHash == null) return BadRequest(new { ok = false, error = "Set a PIN on this device first." });
            return Ok(new { ok = true, deviceKey = await EnsureDeviceKey(hash, row.DeviceKey) });
        }

        internal static string PinProblem(string pin)
        {
            if (string.IsNullOrEmpty(pin) || !System.Text.RegularExpressions.Regex.IsMatch(pin, @"^\d{4,6}$"))
                return "Your PIN must be 4 to 6 digits.";
            if (pin.Distinct().Count() == 1) return "Choose a PIN that isn't the same digit repeated.";
            const string up = "0123456789012345", down = "9876543210987654";
            if (up.Contains(pin) || down.Contains(pin)) return "Choose a PIN that isn't a simple sequence like 1234.";
            return null;
        }

        private static string HashPin(string pin)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin), salt, PinIterations, HashAlgorithmName.SHA256, 32);
            return $"v1${PinIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
        }

        private static bool VerifyPin(string pin, string stored)
        {
            try
            {
                var parts = stored.Split('$');
                if (parts.Length != 4 || parts[0] != "v1") return false;
                var iterations = int.Parse(parts[1]);
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                var actual = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(pin ?? ""), salt, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            catch { return false; }
        }
        public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
        public record ForgotRequest(string Email);
        public record ResetRequest(string Email, string Token, string NewPassword, string Code);
        private const string MustChangeClaim = "must_change_password";

        // ---------- Two-factor (authenticator app) ----------

        [HttpGet("2fa")]
        public async Task<IActionResult> TwoFactorStatus()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            return Ok(new { ok = true, enabled = user.TwoFactorEnabled, recoveryCodesLeft = await _users.CountRecoveryCodesAsync(user) });
        }

        // Creates (or recreates) the secret. Returns the key for manual entry plus the otpauth URI.
        [HttpPost("2fa/setup")]
        public async Task<IActionResult> TwoFactorSetup()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (user.TwoFactorEnabled)
                return BadRequest(new { ok = false, error = "Two-factor is already switched on. Turn it off first to set up a new device." });

            await _users.ResetAuthenticatorKeyAsync(user);
            var key = await _users.GetAuthenticatorKeyAsync(user);
            return Ok(new { ok = true, key = FormatKey(key), uri = OtpAuthUri(user, key) });
        }

        [HttpGet("2fa/qr")]
        public async Task<IActionResult> TwoFactorQr()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized();
            var key = await _users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key)) return NotFound();

            using var gen = new QRCoder.QRCodeGenerator();
            using var data = gen.CreateQrCode(OtpAuthUri(user, key), QRCoder.QRCodeGenerator.ECCLevel.Q);
            var png = new QRCoder.PngByteQRCode(data).GetGraphic(6);
            Response.Headers.CacheControl = "no-store";
            return File(png, "image/png");
        }

        [HttpPost("2fa/enable")]
        public async Task<IActionResult> TwoFactorEnable([FromBody] TwoFactorCodeRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (string.IsNullOrEmpty(await _users.GetAuthenticatorKeyAsync(user)))
                return BadRequest(new { ok = false, error = "Start the setup first." });

            var code = CleanCode(req?.Code);
            if (!await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, code))
                return BadRequest(new { ok = false, error = "That code isn't right. Check the time on your phone and try the newest code." });

            await _users.SetTwoFactorEnabledAsync(user, true);
            var codes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
            await LogLogin(user.UserName, user.Id, "2fa-on", true, null);
            return Ok(new { ok = true, recoveryCodes = codes });
        }

        [HttpPost("2fa/disable")]
        public async Task<IActionResult> TwoFactorDisable([FromBody] TwoFactorCodeRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (!user.TwoFactorEnabled) return Ok(new { ok = true });

            if (!await CheckSecondFactor(user, req?.Code))
                return BadRequest(new { ok = false, error = "Enter a current code from your authenticator app, or one of your recovery codes." });

            await _users.SetTwoFactorEnabledAsync(user, false);
            await _users.ResetAuthenticatorKeyAsync(user);
            await LogLogin(user.UserName, user.Id, "2fa-off", true, null);
            return Ok(new { ok = true });
        }

        [HttpPost("2fa/recovery-codes")]
        public async Task<IActionResult> TwoFactorNewRecoveryCodes([FromBody] TwoFactorCodeRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (!user.TwoFactorEnabled) return BadRequest(new { ok = false, error = "Two-factor is not switched on." });
            if (!await CheckSecondFactor(user, req?.Code))
                return BadRequest(new { ok = false, error = "Enter a current code from your authenticator app." });
            var codes = await _users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
            return Ok(new { ok = true, recoveryCodes = codes });
        }

        // Accepts either a 6-digit authenticator code or an unused recovery code.
        private async Task<bool> CheckSecondFactor(ApplicationUser user, string rawCode)
        {
            var code = CleanCode(rawCode);
            if (string.IsNullOrEmpty(code)) return false;
            if (code.Length == 6 && code.All(char.IsDigit)
                && await _users.VerifyTwoFactorTokenAsync(user, _users.Options.Tokens.AuthenticatorTokenProvider, code))
                return true;
            // Recovery codes are issued as xxxxx-xxxxx; accept them with or without the dash.
            var recoveryCode = (rawCode ?? "").Trim().ToUpperInvariant();
            if (!recoveryCode.Contains('-') && recoveryCode.Length == 10)
                recoveryCode = recoveryCode.Insert(5, "-");
            var recovery = await _users.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCode);
            return recovery.Succeeded;
        }

        private static string CleanCode(string code) =>
            (code ?? "").Replace(" ", "").Replace("-", "").Trim();

        private static string FormatKey(string key)
        {
            var k = (key ?? "").ToLowerInvariant();
            return string.Join(" ", Enumerable.Range(0, (k.Length + 3) / 4).Select(i => k.Substring(i * 4, Math.Min(4, k.Length - i * 4))));
        }

        // "image" gives the SmartCube logo in authenticators that support it (2FAS, Aegis, FreeOTP, Ente).
        private const string AuthenticatorImage = "https://api.smartcubemobile.com/Images/smartcube-authenticator.png";

        private static string OtpAuthUri(ApplicationUser user, string key) =>
            $"otpauth://totp/SmartCube:{Uri.EscapeDataString(user.Email ?? user.UserName)}?secret={key}&issuer=SmartCube&digits=6" +
            $"&image={Uri.EscapeDataString(AuthenticatorImage)}";

        // Lets the reset page know whether to ask for an authenticator code (does not consume the token).
        [HttpGet("reset/status")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetStatus([FromQuery] string email, [FromQuery] string token)
        {
            var user = string.IsNullOrWhiteSpace(email) ? null : await _users.FindByEmailAsync(email.Trim());
            if (user == null || string.IsNullOrEmpty(token))
                return Ok(new { ok = true, valid = false, twoFactorRequired = false });
            var valid = await _users.VerifyUserTokenAsync(user, _users.Options.Tokens.PasswordResetTokenProvider,
                UserManager<ApplicationUser>.ResetPasswordTokenPurpose, token);
            return Ok(new { ok = true, valid, twoFactorRequired = valid && user.TwoFactorEnabled });
        }

        // Always answers "ok" so the form can't be used to discover which emails have accounts.
        [HttpPost("forgot")]
        [AllowAnonymous]
        public async Task<IActionResult> Forgot([FromBody] ForgotRequest req,
            [FromServices] Microsoft.AspNetCore.Identity.UI.Services.IEmailSender mail, [FromServices] IConfiguration config)
        {
            var email = (req?.Email ?? "").Trim();
            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
                return BadRequest(new { ok = false, error = "Enter your email address." });
            if (!Services.SmtpEmailSender.IsConfigured(config))
                return StatusCode(503, new { ok = false, error = "Password reset emails aren't set up yet. Please contact support to reset your password." });

            var user = await _users.FindByEmailAsync(email);
            if (user != null)
            {
                var token = await _users.GeneratePasswordResetTokenAsync(user);
                var site = (config["Email:SiteBaseUrl"] ?? "https://api.smartcubemobile.com/site").TrimEnd('/');
                var link = $"{site}/reset-password.html?email={Uri.EscapeDataString(user.Email)}&token={Uri.EscapeDataString(token)}";
                var html = $@"<p>Hello {System.Net.WebUtility.HtmlEncode(user.UserName)},</p>
<p>Someone asked to reset the password for your SmartCube Mobile account. If that was you, click the link below within the next hour:</p>
<p><a href=""{link}"">Reset my password</a></p>
<p>If you didn't ask for this, you can ignore this email — your password won't change.</p>
<p>SmartCube Mobile</p>";
                try { await mail.SendEmailAsync(user.Email, "Reset your SmartCube Mobile password", html); }
                catch (Exception ex)
                {
                    await LogLogin(email, user.Id, "forgot", false, "Email send failed: " + ex.Message);
                    return StatusCode(502, new { ok = false, error = "We couldn't send the email just now. Please try again later or contact support." });
                }
                await LogLogin(email, user.Id, "forgot", true, null);
            }
            else
            {
                await LogLogin(email, null, "forgot", false, "Unknown email");
            }
            return Ok(new { ok = true });
        }

        [HttpPost("reset")]
        [AllowAnonymous]
        public async Task<IActionResult> Reset([FromBody] ResetRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Email) || string.IsNullOrWhiteSpace(req.Token) || string.IsNullOrEmpty(req.NewPassword))
                return BadRequest(new { ok = false, errors = new[] { "Email, reset link and new password are all required." } });

            var user = await _users.FindByEmailAsync(req.Email.Trim());
            if (user == null)
                return BadRequest(new { ok = false, errors = new[] { "This reset link is not valid." } });

            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(req.Code))
                    return BadRequest(new { ok = false, twoFactorRequired = true, errors = new[] { "Enter the code from your authenticator app (or a recovery code)." } });
                if (!await CheckSecondFactor(user, req.Code))
                {
                    await LogLogin(user.UserName, user.Id, "reset", false, "Bad two-factor code");
                    return BadRequest(new { ok = false, twoFactorRequired = true, errors = new[] { "That authenticator code isn't right." } });
                }
            }

            var result = await _users.ResetPasswordAsync(user, req.Token, req.NewPassword);
            if (!result.Succeeded)
            {
                var msgs = result.Errors.Select(e => e.Code == "InvalidToken" ? "This reset link has expired or already been used. Request a new one." : e.Description).ToArray();
                return BadRequest(new { ok = false, errors = msgs });
            }

            var claims = await _users.GetClaimsAsync(user);
            foreach (var c in claims.Where(c => c.Type == MustChangeClaim).ToList())
                await _users.RemoveClaimAsync(user, c);
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.DeviceTokens SET Revoked = 1 WHERE UserId = {user.Id}");
            await LogLogin(user.UserName, user.Id, "reset", true, null);
            return Ok(new { ok = true });
        }

        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { ok = false, error = "Sign in first." });
            if (string.IsNullOrEmpty(req?.NewPassword))
                return BadRequest(new { ok = false, errors = new[] { "New password is required." } });

            var result = await _users.ChangePasswordAsync(user, req.CurrentPassword ?? "", req.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { ok = false, errors = result.Errors.Select(e => e.Description).ToArray() });

            var claims = await _users.GetClaimsAsync(user);
            foreach (var c in claims.Where(c => c.Type == MustChangeClaim).ToList())
                await _users.RemoveClaimAsync(user, c);

            await _signIn.RefreshSignInAsync(user);
            return Ok(new { ok = true });
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            var username = (req.Username ?? "").Trim();
            var email = (req.Email ?? "").Trim();
            if (username.Length < 3 || username.Length > 18)
                return BadRequest(new { ok = false, errors = new[] { "Username must be 3 to 18 characters." } });
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return BadRequest(new { ok = false, errors = new[] { "A valid email address is required." } });
            if (string.IsNullOrEmpty(req.Password))
                return BadRequest(new { ok = false, errors = new[] { "Password is required." } });

            if (await _users.FindByEmailAsync(email) != null)
                return Conflict(new { ok = false, errors = new[] { "An account with that email already exists." } });

            var now = DateTime.UtcNow;
            var user = new ApplicationUser
            {
                UserName = username,
                Email = email,
                Expiration1 = now.AddDays(30),
                Expiration2 = now.AddDays(30),
                Expiration3 = now,
                Subscriber = false,
                Trace = true,
                MultipleMeter = false,
                Administrator = false,
            };

            var result = await _users.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return BadRequest(new { ok = false, errors = result.Errors.Select(e => e.Description).ToArray() });

            if (!string.IsNullOrWhiteSpace(req.FullName))
                await _users.AddClaimAsync(user, new Claim(FullNameClaim, req.FullName.Trim()));

            await _signIn.SignInAsync(user, isPersistent: false);
            return Ok(await UserInfo(user));
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var login = (req.Login ?? "").Trim();
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(req.Password))
                return BadRequest(new { ok = false, error = "Username/email and password are required." });

            ApplicationUser user = login.Contains('@')
                ? await _users.FindByEmailAsync(login)
                : await _users.FindByNameAsync(login);
            if (user == null)
            {
                await LogLogin(login, null, "password", false, "Unknown user");
                return Unauthorized(new { ok = false, error = "Invalid login attempt." });
            }

            var check = await _signIn.CheckPasswordSignInAsync(user, req.Password, lockoutOnFailure: false);
            if (!check.Succeeded)
            {
                var why = check.IsLockedOut ? "Account is locked out." : "Invalid login attempt.";
                await LogLogin(login, user.Id, "password", false, why);
                return Unauthorized(new { ok = false, error = why });
            }

            if (user.TwoFactorEnabled)
            {
                if (string.IsNullOrWhiteSpace(req.Code))
                    return Ok(new { ok = false, twoFactorRequired = true, error = "Enter the 6-digit code from your authenticator app." });
                if (!await CheckSecondFactor(user, req.Code))
                {
                    await LogLogin(login, user.Id, "2fa", false, "Bad two-factor code");
                    return Unauthorized(new { ok = false, twoFactorRequired = true, error = "That code isn't right. Try the newest code from your app." });
                }
            }

            await _signIn.SignInAsync(user, isPersistent: req.Remember);

            user.LastLogOnTime = DateTime.UtcNow;
            await _users.UpdateAsync(user);
            await LogLogin(login, user.Id, "password", true, null);
            return Ok(await UserInfo(user));
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signIn.SignOutAsync();
            return Ok(new { ok = true });
        }

        [HttpGet("me")]
        [AllowAnonymous]
        public async Task<IActionResult> Me()
        {
            var user = await _users.GetUserAsync(User);
            if (user == null)
                return Ok(new { ok = false });
            return Ok(await UserInfo(user));
        }

        [HttpPost("device/register")]
        public async Task<IActionResult> DeviceRegister([FromBody] DeviceRegisterRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { ok = false, error = "Sign in first." });

            // A PIN is optional here only so older app versions keep working; the current app always sends one.
            string pinHash = null;
            if (!string.IsNullOrEmpty(req?.Pin))
            {
                var problem = PinProblem(req.Pin);
                if (problem != null) return BadRequest(new { ok = false, error = problem });
                pinHash = HashPin(req.Pin);
            }

            var token = Base64Url(RandomNumberGenerator.GetBytes(32));
            var hash = Sha256(token);
            var device = (req?.DeviceName ?? "").Trim();
            if (device.Length > 100) device = device[..100];

            var deviceKeyStored = pinHash != null ? NewDeviceKey() : null;
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO dbo.DeviceTokens (TokenHash, UserId, DeviceName, PinHash, DeviceKey) VALUES ({hash}, {user.Id}, {(object)(device.Length == 0 ? null : device) ?? DBNull.Value}, {(object)pinHash ?? DBNull.Value}, {(object)deviceKeyStored ?? DBNull.Value})");

            return Ok(new { ok = true, token, hasPin = pinHash != null, deviceKey = OpenDeviceKey(deviceKeyStored) });
        }

        // Set or change the PIN on this remembered device (signed-in user, their own device only).
        [HttpPost("device/pin")]
        public async Task<IActionResult> DevicePin([FromBody] DevicePinRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            if (string.IsNullOrWhiteSpace(req?.Token)) return BadRequest(new { ok = false, error = "This device isn't remembered." });
            var problem = PinProblem(req.Pin);
            if (problem != null) return BadRequest(new { ok = false, error = problem });

            var hash = Sha256(req.Token.Trim());
            var n = await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.DeviceTokens SET PinHash = {HashPin(req.Pin)}, PinFailures = 0 WHERE TokenHash = {hash} AND UserId = {user.Id} AND Revoked = 0");
            if (n == 0) return BadRequest(new { ok = false, error = "This device is no longer remembered. Sign in with your password and tick Remember this device." });
            await LogLogin(user.UserName, user.Id, "device-pin", true, null);
            var stored = await _db.Database.SqlQuery<string>($"SELECT DeviceKey AS [Value] FROM dbo.DeviceTokens WHERE TokenHash = {hash}").FirstOrDefaultAsync();
            return Ok(new { ok = true, deviceKey = await EnsureDeviceKey(hash, stored) });
        }

        [HttpPost("device/login")]
        [AllowAnonymous]
        public async Task<IActionResult> DeviceLogin([FromBody] DeviceLoginRequest req)
        {
            if (string.IsNullOrWhiteSpace(req?.Token))
                return BadRequest(new { ok = false, error = "Token required." });

            var hash = Sha256(req.Token.Trim());
            var row = await _db.Database
                .SqlQuery<DeviceRow>($"SELECT UserId, PinHash, PinFailures, DeviceKey FROM dbo.DeviceTokens WHERE TokenHash = {hash} AND Revoked = 0")
                .FirstOrDefaultAsync();
            if (row == null)
            {
                await LogLogin(null, null, "device", false, "Unknown or revoked device token");
                return Unauthorized(new { ok = false, error = "This device is no longer remembered. Please sign in." });
            }
            var userId = row.UserId;

            if (row.PinHash != null)
            {
                if (string.IsNullOrEmpty(req.Pin))
                    return Ok(new { ok = false, pinRequired = true, attemptsLeft = MaxPinFailures - row.PinFailures, error = "Enter your PIN." });

                if (!VerifyPin(req.Pin, row.PinHash))
                {
                    var failures = row.PinFailures + 1;
                    if (failures >= MaxPinFailures)
                    {
                        await _db.Database.ExecuteSqlInterpolatedAsync(
                            $"UPDATE dbo.DeviceTokens SET PinFailures = {failures}, Revoked = 1 WHERE TokenHash = {hash}");
                        await LogLogin(null, userId, "device-pin", false, "Too many wrong PINs; device forgotten");
                        return Unauthorized(new { ok = false, revoked = true, error = "Too many wrong PINs. This device has been forgotten, so please sign in with your password." });
                    }
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE dbo.DeviceTokens SET PinFailures = {failures} WHERE TokenHash = {hash}");
                    await LogLogin(null, userId, "device-pin", false, "Wrong PIN");
                    var left = MaxPinFailures - failures;
                    return Unauthorized(new { ok = false, pinRequired = true, attemptsLeft = left,
                        error = $"That PIN isn't right. {left} attempt{(left == 1 ? "" : "s")} left before this device is forgotten." });
                }
                if (row.PinFailures > 0)
                    await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.DeviceTokens SET PinFailures = 0 WHERE TokenHash = {hash}");
            }

            var user = await _users.FindByIdAsync(userId);
            if (user == null)
                return Unauthorized(new { ok = false, error = "Account not found." });
            if (await _users.IsLockedOutAsync(user))
            {
                await LogLogin(user.UserName, user.Id, "device", false, "Account is locked out.");
                return Unauthorized(new { ok = false, error = "Account is locked out." });
            }

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.DeviceTokens SET LastUsed = SYSUTCDATETIME() WHERE TokenHash = {hash}");
            user.LastLogOnTime = DateTime.UtcNow;
            await _users.UpdateAsync(user);
            await _signIn.SignInAsync(user, isPersistent: true);
            await LogLogin(user.UserName, user.Id, "device", true, null);

            // needsPin: a device remembered before PINs existed; the app asks the user to set one.
            // deviceKey: only released after a correct PIN (devices with a PIN).
            var deviceKey = row.PinHash != null ? await EnsureDeviceKey(hash, row.DeviceKey) : null;
            return Ok(await UserInfo(user, needsPin: row.PinHash == null, deviceKey: deviceKey));
        }

        [HttpPost("device/revoke")]
        [AllowAnonymous]
        public async Task<IActionResult> DeviceRevoke([FromBody] DeviceLoginRequest req)
        {
            if (!string.IsNullOrWhiteSpace(req?.Token))
            {
                var hash = Sha256(req.Token.Trim());
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE dbo.DeviceTokens SET Revoked = 1 WHERE TokenHash = {hash}");
            }
            await _signIn.SignOutAsync();
            return Ok(new { ok = true });
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequest req)
        {
            var user = await _users.GetUserAsync(User);
            if (user == null)
                return Unauthorized(new { ok = false, error = "Sign in first." });

            var plan = (req.Plan ?? "").Trim();
            if (plan != "Basic" && plan != "Pro")
                return BadRequest(new { ok = false, error = "Choose Basic or Pro." });

            var expires = DateTime.UtcNow.AddDays(365);
            user.Subscriber = true;
            user.Expiration1 = expires;
            user.Expiration2 = expires;
            await _users.UpdateAsync(user);

            var claims = await _users.GetClaimsAsync(user);
            var existingPlan = claims.FirstOrDefault(c => c.Type == PlanClaim);
            if (existingPlan != null)
                await _users.RemoveClaimAsync(user, existingPlan);
            await _users.AddClaimAsync(user, new Claim(PlanClaim, plan));

            string licence = null;
            if (plan == "Pro")
            {
                licence = await ActiveLicence(user.Email);
                if (licence == null)
                {
                    licence = NewLicenceKey();
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO dbo.SmartScanLicences (LicenceKey, Email, ScanLimit, Expires) VALUES ({licence}, {user.Email}, 100, {expires})");
                }
                else
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE dbo.SmartScanLicences SET Expires = {expires} WHERE LicenceKey = {licence}");
                }
            }

            return Ok(new { ok = true, username = user.UserName, plan, expires, smartScanLicence = licence });
        }

        [HttpGet("download/status")]
        public async Task<IActionResult> DownloadStatus()
        {
            var file = FindInstaller();
            if (file == null)
                return Ok(new { ok = true, available = false });

            var user = await _users.GetUserAsync(User);
            string url = null;
            if (user != null)
            {
                var expires = DateTimeOffset.UtcNow.AddMinutes(30).ToUnixTimeSeconds();
                var token = $"{user.Id}|{expires}|{Sign($"{user.Id}|{expires}")}";
                url = $"{PublicBase()}/api/account/download?t={Uri.EscapeDataString(token)}";
            }

            var version = System.Text.RegularExpressions.Regex.Match(file.Name, @"\d+(?:\.\d+){1,3}").Value;
            return Ok(new { ok = true, available = true, fileName = file.Name, sizeMb = Math.Round(file.Length / 1048576.0, 1), version, url });
        }

        [HttpGet("download")]
        [AllowAnonymous]
        public async Task<IActionResult> Download([FromQuery] string t)
        {
            string userId = null;
            if (!string.IsNullOrEmpty(t))
            {
                var parts = t.Split('|');
                if (parts.Length == 3 && long.TryParse(parts[1], out var exp)
                    && exp > DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    && CryptographicOperations.FixedTimeEquals(
                        Encoding.UTF8.GetBytes(Sign($"{parts[0]}|{parts[1]}")), Encoding.UTF8.GetBytes(parts[2])))
                    userId = parts[0];
            }
            if (userId == null)
                userId = (await _users.GetUserAsync(User))?.Id;
            if (userId == null)
                return Unauthorized(new { ok = false, error = "Sign in to download." });

            var file = FindInstaller();
            if (file == null)
                return NotFound(new { ok = false, error = "Installer not available yet." });

            var range = Request.Headers["Range"].ToString();
            if (string.IsNullOrEmpty(range) || range.Contains("bytes=0-"))
            {
                try
                {
                    await _db.Database.ExecuteSqlInterpolatedAsync(
                        $"INSERT INTO dbo.DownloadLog (UserId, FileName, IP) VALUES ({userId}, {file.Name}, {ClientIp()})");
                }
                catch { }
            }
            return PhysicalFile(file.FullName, "application/octet-stream", file.Name, enableRangeProcessing: true);
        }

        private async Task LogLogin(string login, string userId, string method, bool success, string error)
        {
            try
            {
                var version = Request.Headers["X-SmartCube-Version"].ToString();
                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.LoginLog (Login, UserId, Method, Success, Error, AppVersion, IP) VALUES ({(object)login ?? DBNull.Value}, {(object)userId ?? DBNull.Value}, {method}, {success}, {(object)error ?? DBNull.Value}, {(object)(string.IsNullOrEmpty(version) ? null : version) ?? DBNull.Value}, {ClientIp()})");
            }
            catch { }
        }

        private string ClientIp()
        {
            var cf = Request.Headers["CF-Connecting-IP"].ToString();
            if (!string.IsNullOrEmpty(cf)) return cf;
            var fwd = Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrEmpty(fwd)) return fwd.Split(',')[0].Trim();
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        private string PublicBase()
        {
            var configured = HttpContext.RequestServices.GetService<IConfiguration>()?["PublicBaseUrl"];
            if (!string.IsNullOrWhiteSpace(configured)) return configured.TrimEnd('/');
            var host = Request.Host.Value;
            var scheme = host.StartsWith("localhost", StringComparison.OrdinalIgnoreCase) ? "http" : "https";
            return $"{scheme}://{host}";
        }

        private string Sign(string data)
        {
            var secret = HttpContext.RequestServices.GetService<IConfiguration>()?["Download:Secret"] ?? "smartcube-download";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
        }

        private async Task<object> UserInfo(ApplicationUser user, bool? needsPin = null, string deviceKey = null)
        {
            var claims = await _users.GetClaimsAsync(user);
            return new
            {
                ok = true,
                username = user.UserName,
                email = user.Email,
                fullName = claims.FirstOrDefault(c => c.Type == FullNameClaim)?.Value,
                plan = claims.FirstOrDefault(c => c.Type == PlanClaim)?.Value,
                subscriber = user.Subscriber,
                expires = user.Expiration1,
                lastLogin = user.LastLogOnTime,
                smartScanLicence = await ActiveLicence(user.Email),
                mustChangePassword = claims.Any(c => c.Type == MustChangeClaim),
                twoFactorEnabled = user.TwoFactorEnabled,
                needsPin,
                deviceKey,
            };
        }

        private Task<string> ActiveLicence(string email) =>
            _db.Database
                .SqlQuery<string>($"SELECT TOP 1 LicenceKey AS [Value] FROM dbo.SmartScanLicences WHERE Email = {email} AND IsActive = 1 ORDER BY Created DESC")
                .FirstOrDefaultAsync();

        private FileInfo FindInstaller()
        {
            var dir = new DirectoryInfo(Path.Combine(_env.ContentRootPath, "Installers"));
            if (!dir.Exists) return null;
            return dir.GetFiles()
                .Where(f => f.Extension is ".exe" or ".msix" or ".msixbundle" or ".zip")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string NewLicenceKey()
        {
            const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
            var bytes = RandomNumberGenerator.GetBytes(12);
            var chars = bytes.Select(b => alphabet[b % alphabet.Length]).ToArray();
            return $"SC-{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
        }

        private static string Sha256(string value) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

        private static string Base64Url(byte[] bytes) =>
            Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
