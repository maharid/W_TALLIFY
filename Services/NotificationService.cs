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
        Task NotifyUserAsync(int userId, string title, string message, string type = "info", string? actionUrl = null);
        Task NotifyEventAsync(int eventId, string title, string message, string type = "info", string? actionUrl = null);
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

        public async Task NotifyUserAsync(int userId, string title, string message, string type = "info", string? actionUrl = null)
        {
            // 1. Check Preference
            var user = await _db.Users.FindAsync(userId);
            if (user == null || !user.EnableNotifications) return;

            // 2. Log to DB
            var log = new NotificationLog
            {
                UserId = userId,
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
            await _hubContext.Clients.Group($"User_{userId}")
                .SendAsync("ReceiveNotification", title, message, type, actionUrl);
        }

        public async Task NotifyEventAsync(int eventId, string title, string message, string type = "info", string? actionUrl = null)
        {
            // 1. Notify the Organizer
            var ev = await _db.Events.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev != null && ev.UserId != 0)
            {
                await NotifyUserAsync(ev.UserId, title, message, type, actionUrl);
            }
            
            // 2. Broadcast to everyone currently watching this event (Judges, Scorers, etc.)
            // These connections are added to the Event_{id} group in NotificationHub.OnConnectedAsync
            await _hubContext.Clients.Group($"Event_{eventId}")
                .SendAsync("ReceiveNotification", title, message, type, actionUrl);
        }
    }
}
