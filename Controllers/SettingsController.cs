using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using ProjectTallify.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Linq;
using System;
using Microsoft.AspNetCore.Hosting;
using System.IO;
using System.Collections.Generic;

namespace ProjectTallify.Controllers
{
    public class SettingsController : Controller
    {
        private readonly TallifyDbContext _db;
        private readonly IWebHostEnvironment _webHostEnvironment;

        // 5MB Limit
        private const long MaxFileSizeBytes = 5 * 1024 * 1024;

        public SettingsController(TallifyDbContext db, IWebHostEnvironment webHostEnvironment)
        {
            _db = db;
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.ActiveNav = "Settings";
            ViewBag.HideOrgCard = true; // <- this hides the org card in _Layout

            // Get logged-in organizer's ID
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                // Redirect to login if not logged in (should be handled by auth middleware too)
                return RedirectToAction("Login", "Auth");
            }

            // Fetch organizer data
            var organizer = await _db.Organizers
                .Where(u => u.Id == userId.Value)
                .FirstOrDefaultAsync();

            if (organizer == null)
            {
                // Organizer not found, perhaps session is stale
                return RedirectToAction("Login", "Auth");
            }

            return View(organizer);
        }

        [HttpPost]
        public async Task<IActionResult> UploadPhoto(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("No file selected.");

            // 1. Validate Size (Efficiency: Prevent huge files from bloating local storage)
            if (file.Length > MaxFileSizeBytes)
            {
                return BadRequest("File is too large. Maximum size allowed is 5MB.");
            }

            // 2. Validate file type (Security)
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".jpg" && ext != ".jpeg" && ext != ".png" && ext != ".gif")
            {
                return BadRequest("Invalid file type. Only images are allowed.");
            }

            // 3. Create Uploads Folder if not exists
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // 4. Generate Unique Filename
            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            // 5. Save File
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 6. Return Relative URL
            var fileUrl = $"/uploads/{fileName}";
            return Ok(new { filePath = fileUrl });
        }

        [HttpPost]
        public async Task<IActionResult> UpdateTheme([FromBody] UpdateThemeRequest request)
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Unauthorized();

            var organizer = await _db.Organizers.FindAsync(userId.Value);
            if (organizer == null) return NotFound();

            bool changed = false;

            if (!string.IsNullOrWhiteSpace(request.ThemeColor))
            {
                organizer.ThemeColor = request.ThemeColor;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationName))
            {
                organizer.OrganizationName = request.OrganizationName;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationSubtitle))
            {
                organizer.OrganizationSubtitle = request.OrganizationSubtitle;
                changed = true;
            }

            if (!string.IsNullOrWhiteSpace(request.OrganizationPhotoPath)) 
            {
                // DELETE OLD FILE (Cleanup logic)
                if (!string.IsNullOrEmpty(organizer.OrganizationPhotoPath))
                {
                    DeleteLocalFile(organizer.OrganizationPhotoPath);
                }

                organizer.OrganizationPhotoPath = request.OrganizationPhotoPath;
                changed = true;
            }

            if (request.ProfilePhotoPath != null) 
            {
                // If it's an empty string, it means removal
                if (string.IsNullOrEmpty(request.ProfilePhotoPath))
                {
                    if (!string.IsNullOrEmpty(organizer.ProfilePhotoPath))
                    {
                        DeleteLocalFile(organizer.ProfilePhotoPath);
                    }
                    organizer.ProfilePhotoPath = null; 
                }
                else 
                {
                    // New path provided, delete the old one first
                    if (!string.IsNullOrEmpty(organizer.ProfilePhotoPath))
                    {
                        DeleteLocalFile(organizer.ProfilePhotoPath);
                    }
                    organizer.ProfilePhotoPath = request.ProfilePhotoPath;
                }
                changed = true;
            }

            if (request.EnableNotifications.HasValue && request.EnableNotifications.Value != organizer.EnableNotifications)
            {
                organizer.EnableNotifications = request.EnableNotifications.Value;
                changed = true;
            }

            if (changed)
            {
                await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            
            return Ok(new { success = true, message = "No changes" });
        }

        /// <summary>
        /// Deletes a file from the local wwwroot folder.
        /// SAFETY: Includes a check to ensure we don't delete files uploaded within the last 30 days.
        /// </summary>
        private void DeleteLocalFile(string relativePath)
        {
            try
            {
                // 1. Clean path (remove leading slash if any)
                var path = relativePath.StartsWith("/") ? relativePath.Substring(1) : relativePath;
                
                // 2. Map to physical path
                var fullPath = Path.Combine(_webHostEnvironment.WebRootPath, path);

                if (System.IO.File.Exists(fullPath))
                {
                    // SAFETY: Only delete if the file is older than 30 days
                    var creationTime = System.IO.File.GetCreationTimeUtc(fullPath);
                    if (creationTime < DateTime.UtcNow.AddDays(-30))
                    {
                        System.IO.File.Delete(fullPath);
                        Console.WriteLine($"Cleanup: Deleted orphaned file {fullPath}");
                    }
                    else
                    {
                        Console.WriteLine($"Cleanup Skip: File {fullPath} is recent ({creationTime}). Retained.");
                    }
                }
            }
            catch (Exception ex)
            {
                // Log but don't crash (cleanup is secondary)
                Console.WriteLine($"Cleanup Error: Failed to delete {relativePath}. {ex.Message}");
            }
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

            var organizer = await _db.Organizers.FindAsync(userId.Value);
            if (organizer == null)
            {
                return Json(new { success = false, message = "Organizer not found." });
            }

            // Verify Password
            if (string.IsNullOrWhiteSpace(password) || !VerifyPassword(password, organizer.HashedPassword))
            {
                return Json(new { success = false, reason = "password", message = "Incorrect password." });
            }

            organizer.IsActive = false; // Deactivate the account

            // Audit Log
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = null,
                OrganizerId = organizer.Id,
                UserName = organizer.Email,
                UserRole = "Organizer",
                Action = "Account Deactivated",
                Details = $"Organizer '{organizer.Email}' has deactivated their account.",
                CreatedAt = DateTime.UtcNow
            });

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deactivating account for organizer {organizer.Id} ({organizer.Email}): {ex}");
                return Json(new { success = false, message = "An error occurred while deactivating your account." });
            }

            // Log out the organizer after deactivation
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
                .Where(e => e.OrganizerId == userId.Value && e.IsArchived == true)
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
            if (ev.OrganizerId != userId.Value) return Unauthorized(new { success = false, message = "Unauthorized." });

            ev.IsArchived = false;

            _db.AuditLogs.Add(new AuditLog
            {
                EventId = ev.Id,
                OrganizerId = HttpContext.Session.GetInt32("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Organizer",
                UserRole = "Organizer",
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
            var ev = await _db.Events
                .Include(e => e.Contestants)
                .FirstOrDefaultAsync(e => e.Id == id);
                
            if (ev == null) return NotFound(new { success = false, message = "Event not found." });

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "User not authenticated." });
            }
            if (ev.OrganizerId != userId.Value) return Unauthorized(new { success = false, message = "Unauthorized." });

            // 1. Cleanup Header Image
            if (!string.IsNullOrEmpty(ev.HeaderImage))
            {
                DeleteLocalFile(ev.HeaderImage);
            }

            // 2. Cleanup Contestant Photos
            foreach (var c in ev.Contestants)
            {
                if (!string.IsNullOrEmpty(c.PhotoPath))
                {
                    DeleteLocalFile(c.PhotoPath);
                }
            }

            // Audit log BEFORE deletion
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = ev.Id,
                OrganizerId = HttpContext.Session.GetInt32("UserId"),
                UserName = HttpContext.Session.GetString("UserName") ?? "Organizer",
                UserRole = "Organizer",
                Action = "Permanently deleted event",
                Details = $"Event '{ev.Name}' and its associated files were permanently deleted.",
                CreatedAt = DateTime.UtcNow
            });

            _db.Events.Remove(ev);
            await _db.SaveChangesAsync();
            return Ok(new { success = true, message = "Event and associated files permanently deleted." });
        }

        /// <summary>
        /// SYSTEM UTILITY: Scans the uploads folder and deletes any file not referenced in the DB.
        /// SAFETY: Only deletes files older than 30 days.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> RunDeepCleanup()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return Unauthorized();

            // 1. Get all file paths from DB
            var organizerPhotos = await _db.Organizers.Select(u => u.ProfilePhotoPath).Where(p => p != null).ToListAsync();
            var orgLogos = await _db.Organizers.Select(u => u.OrganizationPhotoPath).Where(p => p != null).ToListAsync();
            var eventHeaders = await _db.Events.Select(e => e.HeaderImage).Where(p => p != null).ToListAsync();
            var contestantPhotos = await _db.Contestants.Select(c => c.PhotoPath).Where(p => p != null).ToListAsync();

            var allDbPaths = organizerPhotos
                .Concat(orgLogos)
                .Concat(eventHeaders)
                .Concat(contestantPhotos)
                .Select(p => p!.TrimStart('/').Replace("/", "\\")) // Normalize for Windows
                .ToHashSet();

            // 2. Scan physical folder
            var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder)) return Ok(new { message = "Uploads folder not found." });

            var filesOnDisk = Directory.GetFiles(uploadsFolder);
            int deletedCount = 0;
            int skippedCount = 0;

            foreach (var filePath in filesOnDisk)
            {
                var fileName = Path.GetFileName(filePath);
                var relativePath = Path.Combine("uploads", fileName);

                if (!allDbPaths.Contains(relativePath))
                {
                    // Found an orphan! Check age.
                    var creationTime = System.IO.File.GetCreationTimeUtc(filePath);
                    if (creationTime < DateTime.UtcNow.AddDays(-30))
                    {
                        System.IO.File.Delete(filePath);
                        deletedCount++;
                    }
                    else
                    {
                        skippedCount++;
                    }
                }
            }

            return Ok(new { 
                success = true, 
                message = $"Deep cleanup complete. Deleted {deletedCount} orphans. Skipped {skippedCount} recent files.",
                totalScanned = filesOnDisk.Length
            });
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
