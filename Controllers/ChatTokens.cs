using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace SmartCubeMobileV2026.Controllers
{
    // Signed "Start live chat" links for support tickets. The link is only sent to the ticket's email
    // address, so holding it proves the person is the customer; no sign-in is needed.
    // Token = "{ticketId}.{expiresUnix}.{signature}", valid for 3 days.
    public static class ChatTokens
    {
        private static string _secret = "";
        private static string _baseUrl = "https://api.smartcubemobile.com";
        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(3);

        public static void Configure(IConfiguration config)
        {
            _secret = config["Support:ChatSecret"] ?? config["Download:Secret"] ?? "";
            _baseUrl = (config["PublicBaseUrl"] ?? _baseUrl).TrimEnd('/');
        }

        private static string Sign(string payload)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes("chat|" + _secret));
            return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=')[..22];
        }

        public static string Make(int ticketId)
        {
            var exp = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
            return $"{ticketId}.{exp}.{Sign($"{ticketId}.{exp}")}";
        }

        public static bool TryValidate(string token, out int ticketId, out string error)
        {
            ticketId = 0; error = "This chat link isn't valid.";
            var parts = (token ?? "").Split('.');
            if (parts.Length != 3 || !int.TryParse(parts[0], out var id) || !long.TryParse(parts[1], out var exp)) return false;
            var expected = Sign($"{id}.{exp}");
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(parts[2]))) return false;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > exp) { error = "This chat link has expired. Reply to our email and we'll send a new one."; return false; }
            ticketId = id; error = null;
            return true;
        }

        public static string ChatUrl(int ticketId) => $"{_baseUrl}/site/chat.html?t={Uri.EscapeDataString(Make(ticketId))}";

        // Staff reply email; with a chat link it carries a "Start live chat" button.
        public static string ReplyEmail(string name, string reply, string reference, string status, string chatUrl)
        {
            var button = chatUrl == null ? "" : $@"
<p style=""margin:22px 0"">
  <a href=""{chatUrl}"" style=""background:#2563EB;color:#ffffff;text-decoration:none;padding:12px 22px;border-radius:8px;font-weight:bold;display:inline-block"">Start live chat</a>
</p>
<p style=""color:#666;font-size:13px"">The button opens a chat with SmartCube support about this ticket. It works for 3 days; after that just reply to this email.</p>";
            return $@"<p>Hello {WebUtility.HtmlEncode(name ?? "")},</p>
<div>{WebUtility.HtmlEncode(reply).Replace("\n", "<br>")}</div>{button}
<p style=""color:#666"">Ticket {reference} &middot; status: {WebUtility.HtmlEncode(status)}</p>
<p>SmartCube Support</p>";
        }
    }
}
