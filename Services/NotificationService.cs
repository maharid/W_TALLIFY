using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using ProjectTallify.Hubs;
using ProjectTallify.Models;
using Microsoft.EntityFrameworkCore;

namespace ProjectTallify.Services
{
    public interface INotificationService
    {
        Task NotifyOrganizerAsync(int organizerId, string title, string message, string type = "info", string? actionUrl = null);
        Task NotifyEventAsync(int eventId, string title, string message, string type = "info", string? actionUrl = null);
        Task NotifyJudgeStatusUpdateAsync(int eventId, int judgeId, string status, bool isVerified, bool isAccessSent);
        Task NotifyJudgePulseAsync(int eventId);
        Task NotifyScoresUpdatedAsync(int eventId);
    }

    public class NotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private readonly TallifyDbContext _db;

        public NotificationService(IHubContext<NotificationHub> hubContext, TallifyDbContext db)
        {
            _hubContext = hubContext;
            _db = db;
        }

        public async Task NotifyScoresUpdatedAsync(int eventId)
        {
            // 1. Broadcast to the generic event group (Judges, Scorers watching the dashboard)
            await _hubContext.Clients.Group($"Event_{eventId}")
                .SendAsync("ReceiveScoresUpdate");

            // 2. Also broadcast to the Organizer specifically
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev != null && ev.OrganizerId != 0)
            {
                await _hubContext.Clients.Group($"Organizer_{ev.OrganizerId}")
                    .SendAsync("ReceiveScoresUpdate");
            }
        }

        public async Task NotifyOrganizerAsync(int organizerId, string title, string message, string type = "info", string? actionUrl = null)
        {
            // 1. Check Preference
            var organizer = await _db.Organizers.FindAsync(organizerId);
            if (organizer == null || !organizer.EnableNotifications) return;

            // 2. Log to DB
            var log = new NotificationLog
            {
                OrganizerId = organizerId,
                Title = title,
                Message = message,
                Type = type,
                ActionUrl = actionUrl,
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.NotificationLogs.Add(log);
            await _db.SaveChangesAsync();

            // 3. Send via SignalR
            await _hubContext.Clients.Group($"Organizer_{organizerId}")
                .SendAsync("ReceiveNotification", title, message, type, actionUrl);
        }

        public async Task NotifyEventAsync(int eventId, string title, string message, string type = "info", string? actionUrl = null)
        {
            // 1. Notify the Organizer
            var ev = await _db.Events.Include(e => e.Organizer).FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev != null && ev.OrganizerId != 0)
            {
                await NotifyOrganizerAsync(ev.OrganizerId, title, message, type, actionUrl);
            }
            
            // 2. Broadcast to everyone currently watching this event (Judges, Scorers, etc.)
            // These connections are added to the Event_{id} group in NotificationHub.OnConnectedAsync
            await _hubContext.Clients.Group($"Event_{eventId}")
                .SendAsync("ReceiveNotification", title, message, type, actionUrl);
        }

        public async Task NotifyJudgeStatusUpdateAsync(int eventId, int judgeId, string status, bool isVerified, bool isAccessSent)
        {
            // Broadcast specialized message to the event group (Dashboard)
            await _hubContext.Clients.Group($"Event_{eventId}")
                .SendAsync("ReceiveJudgeStatusUpdate", judgeId, status, isVerified, isAccessSent);
        }

        public async Task NotifyJudgePulseAsync(int eventId)
        {
            // Broadcast pulse to all judges in this event
            await _hubContext.Clients.Group($"Event_{eventId}")
                .SendAsync("ReceiveJudgePulse");
        }
    }
}
