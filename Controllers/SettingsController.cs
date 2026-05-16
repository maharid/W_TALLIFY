using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ProjectTallify.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;

namespace ProjectTallify.Controllers
{
    public class SettingsController : Controller
    {
        private readonly TallifyDbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public SettingsController(TallifyDbContext db, IWebHostEnvironment webHostEnvironment)
        {
            _db = db;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveNav = "Settings";
            ViewBag.HideOrgCard = true; // <- this hides the org card in _Layout

            // Get logged-in user's ID
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                // Redirect to login if not logged in (should be handled by auth middleware too)
                return RedirectToAction("Login", "Auth");
            }

            // Fetch user data
            var user = await _db.Users
                .Where(u => u.Id == userId.Value)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                // User not found, perhaps session is stale
                return RedirectToAction("Login", "Auth");
            }

            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file selected.");

            // 1. Validate file type (optional but recommended)
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif")
            {
                return BadRequest("Invalid file type. Only images are allowed.");
            }

            // 2. Create Uploads Folder if not exists
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // 3. Generate Unique Filename
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // 4. Save File
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 5. Return Relative URL
            var fileUrl = $"/uploads/{fileName}";
            return Ok(new { filePath = fileUrl });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Unauthorized();

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null) return NotFound();

            bool changed = false;

            if (!string.IsNullOrWhiteSpace(request.ThemeColor))
            {
                user.ThemeColor = request.ThemeColor;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationName))
            {
                user.OrganizationName = request.OrganizationName;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationSubtitle))
            {
                user.OrganizationSubtitle = request.OrganizationSubtitle;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationPhotoPath)) // Check for photo path update
            {
                user.OrganizationPhotoPath = request.OrganizationPhotoPath;
                changed = true;
            }
            else // If path is empty/null, and it was previously set, assume removal or reset
            {
                if (request.OrganizationPhotoPath != null && !string.IsNullOrEmpty(user.OrganizationPhotoPath)) // Only clear if explicitly sent as empty string/null but present in request logic (requires care, simpler: if not null)
                {
                   // Actually, for simplicity, we assume frontend sends the new path or we skip.
                   // To support clearing, we might need a flag or empty string.
                   // Current logic for org photo was:
                }
            }

            if (request.ProfilePhotoPath != null) // Check if the property was sent in the request
            {
                // If it's an empty string, it means removal
                if (string.IsNullOrEmpty(request.ProfilePhotoPath))
                {
                    user.ProfilePhotoPath = null; // Set to null in DB
                }
                else // Otherwise, it's a new path
                {
                    user.ProfilePhotoPath = request.ProfilePhotoPath;
                }
                changed = true;
            }

            if (request.EnableNotifications.HasValue && request.EnableNotifications.Value != user.EnableNotifications)
            {
                user.EnableNotifications = request.EnableNotifications.Value;
                changed = true;
            }

            if (changed)
            {
                await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            
            return Ok(new { success = true, message = "No changes" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateAccount([FromForm] string password)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Json(new { success = false, message = "User not logged in." });
            }

            var user = await _db.Users.FindAsync(userId.Value);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Verify Password
            if (string.IsNullOrWhiteSpace(password) || !VerifyPassword(password, user.HashedPassword))
            {
                return Json(new { success = false, reason = "password", message = "Incorrect password." });
            }

            user.IsActive = false; // Deactivate the account

            // Audit Log
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = null,
                UserId = user.Id,
                UserName = user.Email,
                UserRole = user.Role,
                Action = "Account Deactivated",
                Details = $"User '{user.Email}' has deactivated their account.",
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deactivating account for user {user.Id} ({user.Email}): {ex}");
                return Json(new { success = false, message = "An error occurred while deactivating your account." });
            }

            // Log out the user after deactivation
            HttpContext.Session.Clear();
            return Json(new { success = true });
        }

        private static string HashPassword(string password)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            var hashOfInput = HashPassword(password);
            return hashOfInput == storedHash;
        }

        // GET: /Settings/GetArchivedEventsPartial
        [HttpGet]
        public async Task<IActionResult> GetArchivedEventsPartial()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var archivedEvents = await _db.Events
                .Where(e => e.UserId == userId.Value && e.IsArchived == true)
                .OrderByDescending(e => e.StartDateTime)
                .ToListAsync();

            return PartialView("~/Views/Settings/_ArchivedEventsList.cshtml", archivedEvents);
        }

        [HttpPost]
        public async Task<IActionResult> RestoreEvent(int id)
        {
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound(new { success = false, message = "Event not found." });

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "User not authenticated." });
            }
            if (ev.UserId != userId.Value) return Unauthorized(new { success = false, message = "Unauthorized." });

            ev.IsArchived = false;

            _db.AuditLogs.Add(new AuditLog
            {
                EventId = ev.Id,
                UserId = HttpContext.Session.GetInt32("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Organizer",
                UserRole = HttpContext.Session.GetString("UserRole") ?? "Organizer",
                Action = "Restored event",
                Details = $"Event '{ev.Name}' was restored from archive.",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Event restored successfully." });
        }

        [HttpPost]
        public async Task<IActionResult> PermanentlyDeleteEvent(int id)
        {
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == id);
            if (ev == null) return NotFound(new { success = false, message = "Event not found." });

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "User not authenticated." });
            }
            if (ev.UserId != userId.Value) return Unauthorized(new { success = false, message = "Unauthorized." });

            // Audit log BEFORE deletion, as ev will be gone
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = ev.Id,
                UserId = HttpContext.Session.GetInt32("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Organizer",
                UserRole = HttpContext.Session.GetString("UserRole") ?? "Organizer",
                Action = "Permanently deleted event",
                Details = $"Event '{ev.Name}' was permanently deleted from archive.",
                CreatedAt = DateTime.UtcNow
            });

            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Event permanently deleted." });
        }
    }

    public class UpdateThemeRequest
    {
        public string? ThemeColor { get; set; }
        public string? OrganizationName { get; set; }
        public string? OrganizationSubtitle { get; set; }
        public string? OrganizationPhotoPath { get; set; }
        public string? ProfilePhotoPath { get; set; }
        public bool? EnableNotifications { get; set; }
    }
}
