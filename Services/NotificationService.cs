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
        Task NotifyUserAsync(int userId, string title, string message, string type = "info");
        Task NotifyEventAsync(int eventId, string title, string message, string type = "info");
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

        public async Task NotifyUserAsync(int userId, string title, string message, string type = "info")
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
                CreatedAt = DateTime.UtcNow,
                IsRead = false
            };
            _db.NotificationLogs.Add(log);
            await _db.SaveChangesAsync();

            // 3. Send via SignalR
            await _hubContext.Clients.Group($"User_{userId}")
                .SendAsync("ReceiveNotification", title, message, type);
        }

        public async Task NotifyEventAsync(int eventId, string title, string message, string type = "info")
        {
            // Notify everyone listening to this event (e.g., Organizer dashboard)
            // This is slightly different from "NotifyUser" because it targets a context (Event)
            // Usually, the Organizer is the one watching the event.
            // We can find the Organizer for this event and notify them.
            
            var ev = await _db.Events.Include(e => e.User).FirstOrDefaultAsync(e => e.Id == eventId);
            if (ev != null && ev.UserId != 0) // Assuming Event has a UserId link to Organizer
            {
                await NotifyUserAsync(ev.UserId, title, message, type);
            }
            
            // Additionally, if we want to broadcast to judges via SignalR group:
            // await _hubContext.Clients.Group($"Event_{eventId}").SendAsync("ReceiveNotification", title, message, type);
            // (Leaving this commented out unless requested for judges too, usually notification is for Organizer)
        }
    }
}
