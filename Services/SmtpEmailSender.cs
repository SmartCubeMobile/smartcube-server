using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;

namespace SmartCubeMobileV2026.Services
{
    // Sends Identity's emails (password reset etc.) through the SMTP server in appsettings "Email".
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<SmtpEmailSender> _log;

        public SmtpEmailSender(IConfiguration config, ILogger<SmtpEmailSender> log)
        {
            _config = config;
            _log = log;
        }

        public static bool IsConfigured(IConfiguration config) =>
            !string.IsNullOrWhiteSpace(config["Email:Host"]) && !string.IsNullOrWhiteSpace(config["Email:From"])
            && (string.IsNullOrWhiteSpace(config["Email:User"]) || !string.IsNullOrWhiteSpace(config["Email:Password"]));

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            if (!IsConfigured(_config))
            {
                _log.LogWarning("Email not configured; dropping message to {Email}: {Subject}", email, subject);
                return;
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_config["Email:From"]));
            message.To.Add(MailboxAddress.Parse(email));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlMessage, TextBody = StripTags(htmlMessage) }.ToMessageBody();

            var port = int.TryParse(_config["Email:Port"], out var p) ? p : 587;
            var secure = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            using var client = new SmtpClient();
            await client.ConnectAsync(_config["Email:Host"], port, secure);
            if (!string.IsNullOrEmpty(_config["Email:User"]))
                await client.AuthenticateAsync(_config["Email:User"], _config["Email:Password"]);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        private static string StripTags(string html) =>
            System.Text.RegularExpressions.Regex.Replace(html ?? "", "<[^>]+>", " ").Replace("&amp;", "&").Trim();
    }
}
