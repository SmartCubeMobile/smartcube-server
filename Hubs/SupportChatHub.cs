using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartCubeMobileV2026.Areas.Identity.Data;
using SmartCubeMobileV2026.Controllers;
using SmartCubeMobileV2026.Data;

namespace SmartCubeMobileV2026.Hubs
{
    // Live support chat, started by support: a staff reply email carries a signed "Start live chat"
    // link (ChatTokens). The customer opens site/chat.html?t=..., which joins that ticket's chat.
    // Every message is a SupportTicketReplies row, so the transcript stays on the ticket.
    //
    // Groups:  "staff"        - admin page connections
    //          "ticket-{id}"  - the customer + any staff watching that chat
    // Client events: Message, Typing, ChatUpdated, ChatStarted, ChatStatus, Presence
    public class SupportChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> StaffConnections = new();   // connId -> admin username
        private static readonly ConcurrentDictionary<string, int> CustomerChats = new();         // connId -> ticket id
        private static readonly ConcurrentDictionary<string, int> StaffWatching = new();         // connId -> ticket id

        public static bool StaffOnline => !StaffConnections.IsEmpty;

        private readonly UserManager<ApplicationUser> _users;
        private readonly ApplicationDbContext _db;
        private readonly IEmailSender _mail;
        private readonly ILogger<SupportChatHub> _log;

        public SupportChatHub(UserManager<ApplicationUser> users, ApplicationDbContext db, IEmailSender mail, ILogger<SupportChatHub> log)
        {
            _users = users; _db = db; _mail = mail; _log = log;
        }

        private class TicketRow { public int Id { get; set; } public string UserId { get; set; } public string Email { get; set; } public string Name { get; set; } public string Area { get; set; } public string Subject { get; set; } public string Message { get; set; } public string Status { get; set; } public string Channel { get; set; } public DateTime CreatedAt { get; set; } }
        private class ReplyRow { public int Id { get; set; } public string Author { get; set; } public bool IsStaff { get; set; } public string Message { get; set; } public DateTime CreatedAt { get; set; } }

        private async Task<ApplicationUser> RequireAdmin()
        {
            var u = Context.User?.Identity?.IsAuthenticated == true ? await _users.GetUserAsync(Context.User) : null;
            if (u == null || !u.Administrator) throw new HubException("Administrator access required.");
            return u;
        }

        private async Task<TicketRow> Ticket(int id) =>
            await _db.Database.SqlQuery<TicketRow>($"SELECT Id, UserId, Email, Name, Area, Subject, Message, Status, Channel, CreatedAt FROM dbo.SupportTickets WHERE Id = {id}").FirstOrDefaultAsync();

        private async Task<List<object>> Transcript(TicketRow t)
        {
            var msgs = new List<object> { new { author = t.Name, isStaff = false, message = t.Message, createdAt = t.CreatedAt } };
            var replies = await _db.Database.SqlQuery<ReplyRow>($"SELECT Id, Author, IsStaff, Message, CreatedAt FROM dbo.SupportTicketReplies WHERE TicketId = {t.Id} ORDER BY Id").ToListAsync();
            msgs.AddRange(replies.Select(r => new { author = r.Author, isStaff = r.IsStaff, message = r.Message, createdAt = r.CreatedAt }));
            return msgs;
        }

        private static string Clean(string s, int max)
        {
            s = (s ?? "").Trim();
            return s.Length > max ? s[..max] : s;
        }

        private static bool CustomerHere(int ticketId) => CustomerChats.Values.Contains(ticketId);
        private static bool StaffHere(int ticketId) => StaffWatching.Values.Contains(ticketId);

        private Task Presence(int ticketId) =>
            Clients.Group($"ticket-{ticketId}").SendAsync("Presence", new { ticketId, customerHere = CustomerHere(ticketId), staffHere = StaffHere(ticketId) });

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            StaffConnections.TryRemove(Context.ConnectionId, out _);
            if (StaffWatching.TryRemove(Context.ConnectionId, out var watched)) await Presence(watched);
            if (CustomerChats.TryRemove(Context.ConnectionId, out var ticketId))
            {
                await Presence(ticketId);
                await Clients.Group("staff").SendAsync("ChatUpdated", new { id = ticketId, customerConnected = CustomerHere(ticketId) });
            }
            await base.OnDisconnectedAsync(exception);
        }

        // ---------------- Customer (via the emailed link) ----------------

        public async Task<object> JoinWithToken(string token)
        {
            if (!ChatTokens.TryValidate(token, out var ticketId, out var error)) throw new HubException(error);
            var t = await Ticket(ticketId) ?? throw new HubException("This ticket no longer exists.");
            if (t.Status == "Closed") throw new HubException("This ticket has been closed. Open a new one from the support page if you still need help.");

            CustomerChats[Context.ConnectionId] = ticketId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
            await Clients.Group("staff").SendAsync("ChatStarted", new { id = ticketId, reference = SupportController.Ref(ticketId), t.Name, t.Email, t.Area, t.Subject, t.Status, customerConnected = true });
            await Presence(ticketId);

            return new
            {
                ticketId, reference = SupportController.Ref(ticketId), t.Subject, t.Area, t.Status, customerName = t.Name,
                staffHere = StaffHere(ticketId), messages = await Transcript(t),
            };
        }

        private int CustomerTicket(int ticketId)
        {
            if (!CustomerChats.TryGetValue(Context.ConnectionId, out var mine) || mine != ticketId)
                throw new HubException("Open the chat from the link in your email first.");
            return mine;
        }

        public async Task SendCustomerMessage(int ticketId, string text)
        {
            CustomerTicket(ticketId);
            var t = await Ticket(ticketId) ?? throw new HubException("This ticket no longer exists.");
            text = Clean(text, 4000);
            if (text.Length == 0) return;

            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.SupportTicketReplies (TicketId, Author, IsStaff, Message, Emailed) VALUES ({ticketId}, {t.Name ?? t.Email}, 0, {text}, 0)");
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SupportTickets SET LastCustomerAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(), Status = CASE WHEN Status IN ('Waiting on customer','Resolved') THEN 'In progress' ELSE Status END WHERE Id = {ticketId}");

            await Clients.Group($"ticket-{ticketId}").SendAsync("Message", new { ticketId, author = t.Name ?? t.Email, isStaff = false, message = text, createdAt = DateTime.UtcNow });
            await Clients.Group("staff").SendAsync("ChatUpdated", new { id = ticketId, lastMessage = text, lastFromStaff = false, customerConnected = true, @new = true });
        }

        public async Task Typing(int ticketId)
        {
            var isStaff = StaffWatching.TryGetValue(Context.ConnectionId, out var w) && w == ticketId;
            var isCustomer = CustomerChats.TryGetValue(Context.ConnectionId, out var c) && c == ticketId;
            if (!isStaff && !isCustomer) return;
            await Clients.OthersInGroup($"ticket-{ticketId}").SendAsync("Typing", new { ticketId, isStaff });
        }

        // ---------------- Staff (admin page) ----------------

        // Chats offered (Channel = 'chat') that aren't resolved or closed.
        public async Task<object> JoinStaff()
        {
            var admin = await RequireAdmin();
            StaffConnections[Context.ConnectionId] = admin.UserName;
            await Groups.AddToGroupAsync(Context.ConnectionId, "staff");

            var chats = await _db.Database.SqlQuery<TicketRow>($"SELECT Id, UserId, Email, Name, Area, Subject, Message, Status, Channel, CreatedAt FROM dbo.SupportTickets WHERE Channel = 'chat' AND Status NOT IN ('Resolved','Closed') ORDER BY CreatedAt DESC").ToListAsync();
            var list = new List<object>();
            foreach (var c in chats)
            {
                var last = await _db.Database.SqlQuery<ReplyRow>($"SELECT TOP 1 Id, Author, IsStaff, Message, CreatedAt FROM dbo.SupportTicketReplies WHERE TicketId = {c.Id} ORDER BY Id DESC").FirstOrDefaultAsync();
                list.Add(new
                {
                    id = c.Id, reference = SupportController.Ref(c.Id), c.Name, c.Email, c.Area, c.Subject, c.Status,
                    lastMessage = last?.Message ?? c.Message, lastFromStaff = last?.IsStaff ?? false, lastAt = last?.CreatedAt ?? c.CreatedAt,
                    customerConnected = CustomerHere(c.Id),
                });
            }
            return new { chats = list };
        }

        public async Task<object> WatchChat(int ticketId)
        {
            await RequireAdmin();
            var t = await Ticket(ticketId) ?? throw new HubException("Chat not found.");
            if (StaffWatching.TryGetValue(Context.ConnectionId, out var old) && old != ticketId)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{old}");
                StaffWatching.TryRemove(Context.ConnectionId, out _);
                await Presence(old);
            }
            StaffWatching[Context.ConnectionId] = ticketId;
            await Groups.AddToGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
            await Presence(ticketId);
            return new
            {
                ticket = new { id = t.Id, reference = SupportController.Ref(t.Id), t.Name, t.Email, t.Area, t.Subject, t.Status, customerConnected = CustomerHere(t.Id) },
                messages = await Transcript(t),
            };
        }

        public async Task UnwatchChat(int ticketId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ticket-{ticketId}");
            StaffWatching.TryRemove(Context.ConnectionId, out _);
            await Presence(ticketId);
        }

        // Staff message. If the customer isn't in the chat right now it's emailed too, with the chat button.
        public async Task<object> SendStaffMessage(int ticketId, string text)
        {
            var admin = await RequireAdmin();
            var t = await Ticket(ticketId) ?? throw new HubException("Chat not found.");
            text = Clean(text, 4000);
            if (text.Length == 0) return new { ok = false };

            var customerHere = CustomerHere(ticketId);
            var emailed = false;
            if (!customerHere)
            {
                try
                {
                    await _mail.SendEmailAsync(t.Email, $"[{SupportController.Ref(ticketId)}] {t.Subject}",
                        ChatTokens.ReplyEmail(t.Name, text, SupportController.Ref(ticketId), t.Status, ChatTokens.ChatUrl(ticketId)));
                    emailed = true;
                }
                catch (Exception ex) { _log.LogWarning(ex, "Chat {Id} reply email failed", ticketId); }
            }

            await _db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO dbo.SupportTicketReplies (TicketId, Author, IsStaff, Message, Emailed) VALUES ({ticketId}, {admin.UserName}, 1, {text}, {emailed})");
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SupportTickets SET LastStaffAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME(), UpdatedBy = {admin.UserName}, Status = CASE WHEN Status = 'Open' THEN 'In progress' ELSE Status END WHERE Id = {ticketId}");

            await Clients.Group($"ticket-{ticketId}").SendAsync("Message", new { ticketId, author = "SmartCube Support", staffName = admin.UserName, isStaff = true, message = text, createdAt = DateTime.UtcNow });
            await Clients.Group("staff").SendAsync("ChatUpdated", new { id = ticketId, lastMessage = text, lastFromStaff = true, customerConnected = customerHere });
            return new { ok = true, emailed, customerConnected = customerHere };
        }

        public async Task SetChatStatus(int ticketId, string status)
        {
            var admin = await RequireAdmin();
            if (!SupportController.Statuses.Contains(status)) throw new HubException("Unknown status.");
            await _db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SupportTickets SET Status = {status}, UpdatedAt = SYSUTCDATETIME(), UpdatedBy = {admin.UserName} WHERE Id = {ticketId}");
            await Clients.Group($"ticket-{ticketId}").SendAsync("ChatStatus", new { ticketId, status });
            await Clients.Group("staff").SendAsync("ChatUpdated", new { id = ticketId, status });
        }
    }
}
