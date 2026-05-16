using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectTallify.Controllers
{
    public class NotificationsController : Controller
    {
        private readonly TallifyDbContext _db;

        public NotificationsController(TallifyDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyNotifications(int limit = 10)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Unauthorized();

            var query = _db.NotificationLogs
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.CreatedAt)
                .AsQueryable();

            if (limit > 0)
            {
                query = query.Take(limit);
            }

            var logs = await query
                .Select(n => new {
                    n.Id,
                    n.Title,
                    n.Message,
                    n.Type,
                    n.IsRead,
                    CreatedAt = n.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss") + "Z"
                })
                .ToListAsync();

            return Json(logs);
        }

        [HttpPost]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequestDto request)
        {
            if (request == null)
            {
                 return BadRequest(new { success = false, message = "Invalid request payload." });
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return StatusCode(401, new { success = false, message = "User not authenticated." });

            try
            {
                int id = request.Id; // Use the ID from the DTO
                Console.WriteLine($"[MarkAsRead] Attempting to find notification with ID: {id} for user: {userId}");
                var log = await _db.NotificationLogs.FindAsync(id);
                
                if (log != null && log.UserId == userId)
                {
                    Console.WriteLine($"[MarkAsRead] Found notification {id}. Marking as read.");
                    log.IsRead = true;
                    Console.WriteLine($"[MarkAsRead] Calling SaveChangesAsync for notification {id}.");
                    await _db.SaveChangesAsync();
                    Console.WriteLine($"[MarkAsRead] SaveChangesAsync completed for notification {id}.");
                }
                else if (log == null)
                {
                    Console.WriteLine($"[MarkAsRead] Notification ID {id} not found for user {userId}.");
                    return NotFound(new { success = false, message = $"Notification with ID {id} not found." });
                }
                else // log.UserId != userId
                {
                    Console.WriteLine($"[MarkAsRead] Unauthorized attempt to mark notification {id} by user {userId}. Owner: {log.UserId}");
                    return StatusCode(403, new { success = false, message = "You are not authorized to mark this notification as read." });
                }
                return Ok(new { success = true }); // Return a success response
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MarkAsRead Error] Error marking notification {(request?.Id.ToString() ?? "null")} as read for user {userId}: {ex}");
                return StatusCode(500, new { success = false, message = "Internal server error while marking notification as read." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> MarkAllAsRead()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return StatusCode(401, new { success = false, message = "User not authenticated." });

            var unreadLogs = await _db.NotificationLogs
                .Where(n => n.UserId == userId && !n.IsRead)
                .ToListAsync();

            if (unreadLogs.Any())
            {
                foreach (var log in unreadLogs)
                {
                    log.IsRead = true;
                }
                await _db.SaveChangesAsync();
            }
            return Ok();
        }
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return Ok(0);

            var count = await _db.NotificationLogs
                .Where(n => n.UserId == userId && !n.IsRead)
                .CountAsync();

            return Ok(count);
        }
    } // This is the closing brace for NotificationsController

    // DTO for MarkAsRead action - Correctly placed within the namespace
    public class MarkAsReadRequestDto
    {
        public int Id { get; set; }
    }
} // This is the closing brace for the namespace
