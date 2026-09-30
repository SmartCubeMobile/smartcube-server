using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Controllers
{
    // Customer support tickets.
    //   Public:  GET  /api/support/areas, POST /api/support/tickets (registered emails only)
    //   Admin:   GET  /api/admin/tickets, GET/POST /api/admin/tickets/{id}
    [ApiController]
    public class SupportController : ControllerBase
    {
        public static readonly string[] Areas =
        {
            "Account & login", "Billing & subscription", "Bank connections", "Utilities & energy bills",
            "Energy tariffs", "Insurance", "Smart Scan", "Crypto & investments", "Download & installation",
            "Website", "Other",
        };
        public static readonly string[] Statuses = { "Open", "In progress", "Waiting on customer", "Resolved", "Closed" };

        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _mail;
        private readonly ILogger<SupportController> _log;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<Hubs.SupportChatHub> _hub;

        public SupportController(UserManager<ApplicationUser> users, ApplicationDbContext db, IEmailSender mail, ILogger<SupportController> log,
            Microsoft.AspNetCore.SignalR.IHubContext<Hubs.SupportChatHub> hub)
        {
            _users = users; _db = db; _mail = mail; _log = log; _hub = hub;
        }

        // Tells any open admin page that a ticket was created or the customer wrote on it.
        private Task NotifyStaff(int id, string kind, string who, string text) =>
            Microsoft.AspNetCore.SignalR.ClientProxyExtensions.SendAsync(_hub.Clients.Group("staff"), "TicketUpdated",
                new { id, reference = Ref(id), kind, who, text = text?.Length > 140 ? text[..140] + "…" : text });

        // ---------------- Signed-in customer (the app) ----------------

        public record MyTicketRequest(string Area, string Subject, string Message);
        public record MyReplyRequest(string Message);

        private async Task<ApplicationUser> Me() => await _users.GetUserAsync(User);

        [HttpGet("api/support/my-tickets")]
        public async Task<IActionResult> MyTickets()
        {
            var me = await Me();
            if (me == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            var rows = await _db.Database.SqlQuery<TicketRow>($@"
SELECT t.Id, t.UserId, t.Email, t.Name, t.Area, t.Subject, t.Message, t.Status, t.AppVersion, t.CreatedAt, t.UpdatedAt, t.UpdatedBy,
       (SELECT COUNT(*) FROM dbo.SupportTicketReplies r WHERE r.TicketId = t.Id) AS Replies, t.Channel
FROM dbo.SupportTickets t WHERE t.UserId = {me.Id} OR t.Email = {me.Email}
ORDER BY COALESCE(t.UpdatedAt, t.CreatedAt) DESC").ToListAsync();
            var staffReplies = await _db.Database.SqlQuery<ReplyRow>($@"
SELECT r.TicketId AS Id, '' AS Author, r.IsStaff, '' AS Message, r.Emailed, MAX(r.CreatedAt) AS CreatedAt
FROM dbo.SupportTicketReplies r JOIN dbo.SupportTickets t ON t.Id = r.TicketId
WHERE r.IsStaff = 1 AND (t.UserId = {me.Id} OR t.Email = {me.Email})
GROUP BY r.TicketId, r.IsStaff, r.Emailed").ToListAsync();
            return Ok(new
            {
                ok = true, areas = Areas,
                tickets = rows.Select(t => new
                {
                    t.Id, reference = Ref(t.Id), t.Area, t.Subject, t.Status, t.Channel, t.CreatedAt, t.UpdatedAt, t.Replies,
                    lastStaffReplyAt = staffReplies.Where(s => s.Id == t.Id).Select(s => (DateTime?)s.CreatedAt).DefaultIfEmpty(null).Max(),
                    chatUrl = t.Channel == "chat" && t.Status is not ("Resolved" or "Closed") ? ChatTokens.ChatUrl(t.Id) : null,
                }),
            });
        }

        [HttpGet("api/support/my-tickets/{id:int}")]
        public async Task<IActionResult> MyTicket(int id)
        {
            var me = await Me();
            if (me == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            var t = await _db.Database.SqlQuery<TicketRow>($"SELECT Id, UserId, Email, Name, Area, Subject, Message, Status, AppVersion, CreatedAt, UpdatedAt, UpdatedBy, 0 AS Replies, Channel FROM dbo.SupportTickets WHERE Id = {id} AND (UserId = {me.Id} OR Email = {me.Email})").FirstOrDefaultAsync();
            if (t == null) return NotFound(new { ok = false, error = "Ticket not found." });
            var replies = await _db.Database.SqlQuery<ReplyRow>($"SELECT Id, Author, IsStaff, Message, Emailed, CreatedAt FROM dbo.SupportTicketReplies WHERE TicketId = {id} ORDER BY Id").ToListAsync();
            return Ok(new
            {
                ok = true,
                ticket = new
                {
                    t.Id, reference = Ref(t.Id), t.Area, t.Subject, t.Message, t.Status, t.Channel, t.CreatedAt, t.UpdatedAt,
                    chatUrl = t.Channel == "chat" && t.Status is not ("Resolved" or "Closed") ? ChatTokens.ChatUrl(t.Id) : null,
                },
                replies = replies.Select(r => new { author = r.IsStaff ? "SmartCube Support" : "You", r.IsStaff, r.Message, r.CreatedAt }),
            });
        }

        [HttpPost("api/support/my-tickets")]
        public async Task<IActionResult> MyNewTicket([FromBody] MyTicketRequest req)
        {
            var me = await Me();
            if (me == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            var fullName = (await _users.GetClaimsAsync(me)).FirstOrDefault(c => c.Type == "full_name")?.Value;
            return await Submit(new TicketRequest(me.Email, fullName ?? me.UserName, req?.Area, req?.Subject, req?.Message));
        }

        [HttpPost("api/support/my-tickets/{id:int}/reply")]
        public async Task<IActionResult> MyReply(int id, [FromBody] MyReplyRequest req)
        {
            var me = await Me();
            if (me == null) return Unauthorized(new { ok = false, error = "Sign in first." });
            var t = await _db.Database.SqlQuery<TicketRow>($"SELECT Id, UserId, Email, Name, Area, Subject, Message, Status, AppVersion, CreatedAt, UpdatedAt, UpdatedBy, 0 AS Replies, Channel FROM dbo.SupportTickets WHERE Id = {id} AND (UserId = {me.Id} OR Email = {me.Email})").FirstOrDefaultAsync();
            if (t == null) return NotFound(new { ok = false, error = "Ticket not found." });
            if (t.Status == "Closed") return BadRequest(new { ok = false, error = "This ticket is closed. Please open a new one." });
            var text = (req?.Message ?? "").Trim();
            if (text.Length < 2) return BadRequest(new { ok = false, error = "Type a reply first." });
            if (text.Length > 8000) text = text[..8000];

            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.SupportTicketReplies (TicketId, Author, IsStaff, Message, Emailed) VALUES ({id}, {t.Name ?? me.UserName}, 0, {text}, 0)");
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SupportTickets SET LastCustomerAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(), Status = CASE WHEN Status IN ('Waiting on customer','Resolved') THEN 'Open' ELSE Status END WHERE Id = {id}");
            await NotifyStaff(id, "reply", t.Name ?? me.UserName, text);
            if (t.Channel == "chat")
                await Microsoft.AspNetCore.SignalR.ClientProxyExtensions.SendAsync(_hub.Clients.Group($"ticket-{id}"), "Message",
                    new { ticketId = id, author = t.Name ?? me.UserName, isStaff = false, message = text, createdAt = DateTime.UtcNow });
            return Ok(new { ok = true });
        }

        public record TicketRequest(string Email, string Name, string Area, string Subject, string Message);
        public record TicketUpdate(string Status, string Reply, bool OfferChat);

        private class TicketRow
        {
            public int Id { get; set; } public string UserId { get; set; } public string Email { get; set; } public string Name { get; set; }
            public string Area { get; set; } public string Subject { get; set; } public string Message { get; set; } public string Status { get; set; }
            public string AppVersion { get; set; } public DateTime CreatedAt { get; set; } public DateTime? UpdatedAt { get; set; } public string UpdatedBy { get; set; }
            public int Replies { get; set; } public string Channel { get; set; }
        }
        private class ReplyRow { public int Id { get; set; } public string Author { get; set; } public bool IsStaff { get; set; } public string Message { get; set; } public bool Emailed { get; set; } public DateTime CreatedAt { get; set; } }
        private class IdRow { public int Value { get; set; } }

        public static string Ref(int id) => $"SC-{id:D5}";

        // ---------------- Public ----------------

        [HttpGet("api/support/areas")]
        [AllowAnonymous]
        public IActionResult GetAreas() => Ok(new { ok = true, areas = Areas });

        // Whether anyone from support is available for live chat right now.
        [HttpGet("api/support/chat/status")]
        [AllowAnonymous]
        public IActionResult ChatStatus() => Ok(new { ok = true, staffOnline = Hubs.SupportChatHub.StaffOnline });

        [HttpPost("api/support/tickets")]
        [AllowAnonymous]
        public async Task<IActionResult> Submit([FromBody] TicketRequest req)
        {
            var email = (req?.Email ?? "").Trim();
            var subject = (req?.Subject ?? "").Trim();
            var message = (req?.Message ?? "").Trim();
            var area = Areas.FirstOrDefault(a => string.Equals(a, req?.Area?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrEmpty(email) || !email.Contains('@'))
                return BadRequest(new { ok = false, error = "Enter the email address on your SmartCube account." });
            if (area == null)
                return BadRequest(new { ok = false, error = "Choose which area the problem is in." });
            if (subject.Length < 3)
                return BadRequest(new { ok = false, error = "Give the ticket a short subject." });
            if (message.Length < 10)
                return BadRequest(new { ok = false, error = "Describe the problem in a sentence or two." });
            if (subject.Length > 200) subject = subject[..200];
            if (message.Length > 8000) message = message[..8000];

            var user = await _users.FindByEmailAsync(email);
            if (user == null)
                return BadRequest(new { ok = false, code = "unknown_email", error = "No records of this email in our database" });

            // Simple flood guard: at most 5 tickets per account per 24 hours.
            var recent = (await _db.Database.SqlQuery<IdRow>($"SELECT COUNT(*) AS Value FROM dbo.SupportTickets WHERE Email = {user.Email} AND CreatedAt >= {DateTime.UtcNow.AddDays(-1)}").FirstOrDefaultAsync())?.Value ?? 0;
            if (recent >= 5)
                return StatusCode(429, new { ok = false, error = "You've opened several tickets today. We'll reply to those first; please add to an existing ticket by replying to our email." });

            var name = string.IsNullOrWhiteSpace(req.Name) ? user.UserName : req.Name.Trim();
            if (name.Length > 120) name = name[..120];
            var version = Request.Headers["X-SmartCube-Version"].FirstOrDefault();
            var ip = Request.Headers["CF-Connecting-IP"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();

            var id = (await _db.Database.SqlQuery<IdRow>($@"
INSERT INTO dbo.SupportTickets (UserId, Email, Name, Area, Subject, Message, Status, AppVersion, Ip)
OUTPUT INSERTED.Id AS Value
VALUES ({user.Id}, {user.Email}, {name}, {area}, {subject}, {message}, 'Open', {version}, {ip})").ToListAsync()).First().Value;

            var reference = Ref(id);
            try
            {
                await _mail.SendEmailAsync(user.Email, $"[{reference}] We've received your support request",
                    $@"<p>Hello {WebUtility.HtmlEncode(name)},</p>
<p>Thanks for getting in touch. Your ticket <b>{reference}</b> has been logged and we'll reply to this email address.</p>
<p><b>Area:</b> {WebUtility.HtmlEncode(area)}<br><b>Subject:</b> {WebUtility.HtmlEncode(subject)}</p>
<blockquote style=""border-left:3px solid #ccc;margin:0;padding:0 12px;color:#555"">{WebUtility.HtmlEncode(message).Replace("\n", "<br>")}</blockquote>
<p>SmartCube Support</p>");
            }
            catch (Exception ex) { _log.LogWarning(ex, "Ticket {Ref} confirmation email failed", reference); }

            try { await NotifyStaff(id, "new", name, subject); } catch { }
            return Ok(new { ok = true, reference, id });
        }

        // ---------------- Admin ----------------

        private ApplicationUser _caller;
        private async Task<ApplicationUser> RequireAdmin()
        {
            _caller = await _users.GetUserAsync(User);
            return _caller != null && _caller.Administrator ? _caller : null;
        }
        private IActionResult Denied() =>
            _caller != null ? StatusCode(403, new { ok = false, error = "Administrator access required." })
                            : Unauthorized(new { ok = false, error = "Sign in as an administrator." });

        [HttpGet("api/admin/tickets")]
        public async Task<IActionResult> List([FromQuery] string status, [FromQuery] string area)
        {
            if (await RequireAdmin() == null) return Denied();
            var rows = await _db.Database.SqlQuery<TicketRow>($@"
SELECT t.Id, t.UserId, t.Email, t.Name, t.Area, t.Subject, t.Message, t.Status, t.AppVersion, t.CreatedAt, t.UpdatedAt, t.UpdatedBy,
       (SELECT COUNT(*) FROM dbo.SupportTicketReplies r WHERE r.TicketId = t.Id) AS Replies, t.Channel
FROM dbo.SupportTickets t
ORDER BY CASE t.Status WHEN 'Open' THEN 0 WHEN 'In progress' THEN 1 WHEN 'Waiting on customer' THEN 2 ELSE 3 END, t.CreatedAt DESC").ToListAsync();

            var counts = rows.GroupBy(r => r.Status).ToDictionary(g => g.Key, g => g.Count());
            var areaCounts = rows.Where(r => r.Status is not ("Resolved" or "Closed")).GroupBy(r => r.Area).ToDictionary(g => g.Key, g => g.Count());
            if (!string.IsNullOrEmpty(status) && status != "All")
                rows = status == "Active" ? rows.Where(r => r.Status is not ("Resolved" or "Closed")).ToList() : rows.Where(r => r.Status == status).ToList();
            if (!string.IsNullOrEmpty(area) && area != "All") rows = rows.Where(r => r.Area == area).ToList();

            return Ok(new
            {
                ok = true, areas = Areas, statuses = Statuses, counts, openByArea = areaCounts,
                tickets = rows.Select(r => new
                {
                    r.Id, reference = Ref(r.Id), r.Email, r.Name, r.Area, r.Subject, r.Status, r.AppVersion,
                    r.CreatedAt, r.UpdatedAt, r.UpdatedBy, r.Replies, r.Channel,
                    preview = r.Message.Length > 140 ? r.Message[..140] + "…" : r.Message,
                }),
            });
        }

        [HttpGet("api/admin/tickets/{id:int}")]
        public async Task<IActionResult> Get(int id)
        {
            if (await RequireAdmin() == null) return Denied();
            var t = await _db.Database.SqlQuery<TicketRow>($"SELECT Id, UserId, Email, Name, Area, Subject, Message, Status, AppVersion, CreatedAt, UpdatedAt, UpdatedBy, 0 AS Replies, Channel FROM dbo.SupportTickets WHERE Id = {id}").FirstOrDefaultAsync();
            if (t == null) return NotFound(new { ok = false, error = "Ticket not found." });
            var replies = await _db.Database.SqlQuery<ReplyRow>($"SELECT Id, Author, IsStaff, Message, Emailed, CreatedAt FROM dbo.SupportTicketReplies WHERE TicketId = {id} ORDER BY Id").ToListAsync();
            var user = t.UserId == null ? null : await _users.FindByIdAsync(t.UserId);
            return Ok(new
            {
                ok = true,
                ticket = new { t.Id, reference = Ref(t.Id), t.Email, t.Name, t.Area, t.Subject, t.Message, t.Status, t.AppVersion, t.CreatedAt, t.UpdatedAt, t.UpdatedBy, t.Channel, userId = t.UserId, username = user?.UserName },
                replies,
            });
        }

        // Change status and/or post a reply. A reply is emailed to the customer.
        [HttpPost("api/admin/tickets/{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] TicketUpdate req)
        {
            var admin = await RequireAdmin();
            if (admin == null) return Denied();
            var t = await _db.Database.SqlQuery<TicketRow>($"SELECT Id, UserId, Email, Name, Area, Subject, Message, Status, AppVersion, CreatedAt, UpdatedAt, UpdatedBy, 0 AS Replies, Channel FROM dbo.SupportTickets WHERE Id = {id}").FirstOrDefaultAsync();
            if (t == null) return NotFound(new { ok = false, error = "Ticket not found." });

            var status = Statuses.Contains(req?.Status) ? req.Status : t.Status;
            var reply = (req?.Reply ?? "").Trim();
            var offerChat = req?.OfferChat == true;
            // An invitation with no written reply gets a standard line.
            if (offerChat && reply.Length == 0)
                reply = "I'm looking at your ticket now and I'd like to sort this out with you in a live chat. Click the button below and I'll be with you.";
            if (reply.Length == 0 && status == t.Status)
                return BadRequest(new { ok = false, error = "Nothing to save: write a reply or change the status." });

            bool emailed = false;
            string emailError = null;
            string chatUrl = null;
            if (reply.Length > 0)
            {
                if (offerChat)
                {
                    chatUrl = ChatTokens.ChatUrl(id);
                    await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SupportTickets SET Channel = 'chat' WHERE Id = {id}");
                    if (status == "Open") status = "In progress";
                }
                try
                {
                    await _mail.SendEmailAsync(t.Email, $"[{Ref(id)}] {t.Subject}", ChatTokens.ReplyEmail(t.Name, reply, Ref(id), status, chatUrl));
                    emailed = true;
                }
                catch (Exception ex) { emailError = ex.Message; _log.LogWarning(ex, "Ticket {Ref} reply email failed", Ref(id)); }

                await _db.Database.ExecuteSqlInterpolatedAsync(
                    $"INSERT INTO dbo.SupportTicketReplies (TicketId, Author, IsStaff, Message, Emailed) VALUES ({id}, {admin.UserName}, 1, {reply}, {emailed})");
            }

            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE dbo.SupportTickets SET Status = {status}, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = {admin.UserName} WHERE Id = {id}");

            return Ok(new { ok = true, status, replied = reply.Length > 0, emailed, emailError, chatOffered = chatUrl != null, chatUrl });
        }
    }
}
