using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using ProjectTallify.Services;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ProjectTallify.Controllers
{
    public class AuthController : Controller
    {
        private readonly TallifyDbContext _db;
        private readonly IEmailSender _emailSender;
        private readonly INotificationService _notificationService;

        private const string RememberMeCookieName = "TallifyRemember";

        public AuthController(TallifyDbContext db, IEmailSender emailSender, INotificationService notificationService)
        {
            _db = db;
            _emailSender = emailSender;
            _notificationService = notificationService;
        }

        // ============ LOGIN (GET) ============
        [HttpGet]
        public async Task<IActionResult> Login(string? mode, string? code, string? pin)
        {
            // If user already logged in in this session AND not in join-mode, send to dashboard
            if (!string.Equals(mode, "join", StringComparison.OrdinalIgnoreCase))
            {
                if (HttpContext.Session.GetString("UserLoggedIn") == "true")
                {
                    return RedirectToAction("Dashboard", "Home");
                }

                // Try remember-me cookie if no session
                var autoUser = await TryAutoLoginFromCookieAsync();
                if (autoUser != null)
                {
                    await SetLoginSession(autoUser);
                    return RedirectToAction("Dashboard", "Home");
                }
            }

            ViewData["Title"] = "Login";
            ViewBag.HideOrgCard = true;
            ViewBag.IsAuthPage = true;

            var fromTemp = TempData["AuthMode"] as string;
            ViewBag.AuthMode = !string.IsNullOrEmpty(fromTemp)
                ? fromTemp
                : (string.IsNullOrWhiteSpace(mode) ? "login" : mode.ToLower());

            ViewBag.JoinCodeFromLink = code;
            ViewBag.JoinPinFromLink = pin;
            ViewBag.ResendEmail = TempData["ResendEmail"] as string;

            return View();
        }

        // ============ LOGIN (POST) ============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                TempData["AuthError"] = "Please enter both email and password.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            email = email.Trim();

            // Look up organizer by email (regardless of active status initially)
            var organizer = await _db.Organizers
                .FirstOrDefaultAsync(u => u.Email == email);

            // 1) Email not found at all
            if (organizer == null)
            {
                TempData["AuthError"] = "No account found for this email. Please sign up first.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            // 2) Check if account is deactivated - MOVED AFTER PASSWORD CHECK
            // We want to verify password first so we don't leak account status or allow reactivation without proof of ownership.

            // 3) Email exists but not verified yet
            if (!organizer.EmailConfirmed)
            {
                // ... (existing email confirmation logic) ...
                if (organizer.EmailVerificationTokenExpiresAt.HasValue &&
                    organizer.EmailVerificationTokenExpiresAt.Value < DateTime.UtcNow)
                {
                    organizer.EmailVerificationToken = GenerateEmailToken();
                    organizer.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                    await _db.SaveChangesAsync();

                    var confirmationLink = Url.Action(
                        "ConfirmEmail",
                        "Auth",
                        new { userId = organizer.Id, token = organizer.EmailVerificationToken },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailConfirmationAsync(organizer, confirmationLink!);

                    TempData["AuthError"] =
                        "The previous verification link expired. " +
                        "We sent a new verification email. Please verify within 15 minutes.";
                }
                else
                {
                    TempData["AuthError"] =
                        "Please verify your email address before logging in. " +
                        "If you didn’t get the verification email, your address might be wrong or inactive—please try another one.";
                }

                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            // 4) Password wrong (generic message)
            if (!VerifyPassword(password, organizer.HashedPassword))
            {
                TempData["AuthError"] = "Incorrect email or password.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }
            
            // NOW check Deactivation status
            if (!organizer.IsActive)
            {
                // Password is correct, but account is inactive.
                // Prompt for reactivation.
                ViewBag.ShowReactivationModal = true;
                ViewBag.ReactivationUserId = organizer.Id;
                ViewBag.AuthMode = "login"; // Ensure we stay on login tab
                
                // We need to return View directly to pass ViewBag (Redirect kills it)
                // Re-populate standard view bags
                ViewData["Title"] = "Login";
                ViewBag.HideOrgCard = true;
                ViewBag.IsAuthPage = true;
                return View("Login");
            }

            // 5) Success – set session + optionally remember-me cookie, go to Dashboard
            await SetLoginSession(organizer);
            await HandleRememberMeAsync(organizer, rememberMe);

            return RedirectToAction("Dashboard", "Home");
        }

        // ============ REACTIVATE ACCOUNT (POST) ============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateAccount(int userId)
        {
            var organizer = await _db.Organizers.FindAsync(userId);
            if (organizer == null)
            {
                TempData["AuthError"] = "Organizer not found.";
                return RedirectToAction("Login");
            }

            // Reactivate
            organizer.IsActive = true;
            
            // Audit Log
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = null,
                OrganizerId = organizer.Id,
                UserName = organizer.Email,
                UserRole = "Organizer",
                Action = "Account Reactivated",
                Details = $"Organizer '{organizer.Email}' reactivated their account.",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // Log them in immediately
            await SetLoginSession(organizer);

            TempData["AuthSuccess"] = "Welcome back! Your account has been reactivated.";
            return RedirectToAction("Dashboard", "Home");
        }

        // ============ REGISTER ORGANIZER (POST) ============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterOrganizer(
            string firstName,
            string? lastName,
            string email,
            string password,
            string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["AuthError"] = "First name, email, and password are required.";
                TempData["AuthMode"] = "signup";
                return RedirectToAction("Login");
            }

            if (password != confirmPassword)
            {
                TempData["AuthError"] = "Passwords do not match.";
                TempData["AuthMode"] = "signup";
                return RedirectToAction("Login");
            }

            firstName = firstName.Trim();
            lastName = string.IsNullOrWhiteSpace(lastName) ? null : lastName.Trim();
            email = email.Trim();

            // Email uniqueness
            var existing = await _db.Organizers.FirstOrDefaultAsync(u => u.Email == email);

            if (existing != null)
            {
                if (existing.EmailConfirmed && existing.IsActive)
                {
                    TempData["AuthError"] =
                        "An account with this email already exists. Please log in.";
                    TempData["AuthMode"] = "login";
                    return RedirectToAction("Login");
                }

                TempData["AuthError"] =
                    "This email address was previously used but never successfully verified, " +
                    "or the account is inactive. Please use a valid, active email address to sign up.";
                TempData["AuthMode"] = "signup";
                return RedirectToAction("Login");
            }

            var organizer = new Organizer
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                HashedPassword = HashPassword(password),
                EmailConfirmed = false,
                IsActive = true,
                EmailVerificationToken = GenerateEmailToken(),
                EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            _db.Organizers.Add(organizer);
            await _db.SaveChangesAsync();

            var confirmationLink = Url.Action(
                "ConfirmEmail",
                "Auth",
                new { userId = organizer.Id, token = organizer.EmailVerificationToken },
                protocol: Request.Scheme);

            await _emailSender.SendEmailConfirmationAsync(organizer, confirmationLink!);

            TempData["AuthSuccess"] =
                "We sent a verification link to your email. " +
                "If you didn’t get the verification email, your address might be wrong or inactive—please try another one.";
            TempData["AuthMode"] = "login";
            return RedirectToAction("Login");
        }

        // ============ CONFIRM EMAIL (GET) ============
        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(int userId, string token)
        {
            var organizer = await _db.Organizers.FindAsync(userId);

            if (organizer == null ||
                string.IsNullOrWhiteSpace(organizer.EmailVerificationToken) ||
                !string.Equals(organizer.EmailVerificationToken, token, StringComparison.Ordinal))
            {
                TempData["AuthError"] = "Invalid verification link.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            // Expired → automatically send a new one
            if (organizer.EmailVerificationTokenExpiresAt.HasValue &&
                organizer.EmailVerificationTokenExpiresAt.Value < DateTime.UtcNow)
            {
                organizer.EmailVerificationToken = GenerateEmailToken();
                organizer.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                await _db.SaveChangesAsync();

                var newLink = Url.Action(
                    "ConfirmEmail",
                    "Auth",
                    new { userId = organizer.Id, token = organizer.EmailVerificationToken },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailConfirmationAsync(organizer, newLink!);

                TempData["AuthError"] =
                    "This verification link has expired. A new verification email has been sent. " +
                    "Please verify within 15 minutes.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            organizer.EmailConfirmed = true;
            organizer.EmailVerificationToken = null;
            organizer.EmailVerificationTokenExpiresAt = null;
            await _db.SaveChangesAsync();

            TempData["AuthSuccess"] = "Your email has been verified. You can now log in.";
            TempData["AuthMode"] = "login";
            return RedirectToAction("Login");
        }

        // ============ FORGOT PASSWORD (GET) ============
        [HttpGet]
        public IActionResult ForgotPassword()
        {
            ViewData["Title"] = "Forgot Password";
            ViewBag.HideOrgCard = true;
            ViewBag.IsAuthPage = true;
            return View();
        }

        // ============ FORGOT PASSWORD (POST) ============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Please enter your email.";
                return View();
            }

            email = email.Trim();

            // Look for active, confirmed organizer with this email
            var organizer = await _db.Organizers
                .FirstOrDefaultAsync(u => u.IsActive && u.Email == email && u.EmailConfirmed);

            if (organizer != null)
            {
                organizer.PasswordResetToken = GenerateRandomToken();
                organizer.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                await _db.SaveChangesAsync();

                var resetLink = Url.Action(
                    "ResetPassword",
                    "Auth",
                    new { userId = organizer.Id, token = organizer.PasswordResetToken },
                    protocol: Request.Scheme);

                await _emailSender.SendPasswordResetAsync(organizer, resetLink!);
            }

            // Always show generic response
            TempData["AuthSuccess"] =
                "If an account with that email exists, we’ve sent a password reset link.";
            TempData["AuthMode"] = "login";
            return RedirectToAction("Login");
        }

        // ============ RESET PASSWORD (GET) ============
        [HttpGet]
        public async Task<IActionResult> ResetPassword(int userId, string token)
        {
            var organizer = await _db.Organizers.FindAsync(userId);

            if (organizer == null ||
                string.IsNullOrWhiteSpace(organizer.PasswordResetToken) ||
                !string.Equals(organizer.PasswordResetToken, token, StringComparison.Ordinal) ||
                !organizer.PasswordResetTokenExpiresAt.HasValue ||
                organizer.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
            {
                ViewBag.Error = "This password reset link is invalid or has expired. Please request a new one.";
                return View(model: null);
            }

            // Link is valid → show the form
            ViewBag.UserId = userId;
            ViewBag.Token = token;
            return View();
        }

        // ============ RESET PASSWORD (POST) ============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int userId, string token, string password, string confirmPassword)
        {
            var organizer = await _db.Organizers.FindAsync(userId);

            // 1. Validate that the link is still valid
            if (organizer == null ||
                string.IsNullOrWhiteSpace(organizer.PasswordResetToken) ||
                !string.Equals(organizer.PasswordResetToken, token, StringComparison.Ordinal) ||
                !organizer.PasswordResetTokenExpiresAt.HasValue ||
                organizer.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
            {
                ViewBag.Error = "This password reset link is invalid or has expired. Please request a new one.";
                return View(model: null);
            }

            // 2. Validate fields
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Error = "Please enter and confirm your new password.";
                ViewBag.UserId = userId;
                ViewBag.Token = token;
                return View();
            }

            // 3. Strength rules (same as client-side)
            var strengthError = ValidatePasswordStrength(password);
            if (strengthError != null)
            {
                ViewBag.Error = strengthError;
                ViewBag.UserId = userId;
                ViewBag.Token = token;
                return View();
            }

            // 4. Match check
            if (password != confirmPassword)
            {
                ViewBag.Error = "New password and confirmation do not match.";
                ViewBag.UserId = userId;
                ViewBag.Token = token;
                return View();
            }

            // 5. Success – update password and clear tokens
            organizer.HashedPassword = HashPassword(password);
            organizer.PasswordResetToken = null;
            organizer.PasswordResetTokenExpiresAt = null;
            organizer.RememberMeToken = null;
            organizer.RememberMeTokenExpiresAt = null;

            await _db.SaveChangesAsync();

            TempData["AuthSuccess"] = "Your password has been reset. You can now log in.";
            TempData["AuthMode"] = "login";
            return RedirectToAction("Login");
        }

        // ============ JOIN EVENT (JUDGE / SCORER) ============
        // This matches your Login.cshtml: asp-action="JoinEvent" with eventCode + personalPin
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinEvent(string eventCode, string personalPin)
        {
            ViewData["Title"] = "Login";
            ViewBag.HideOrgCard = true;
            ViewBag.IsAuthPage = true;

            var code = (eventCode ?? string.Empty).Trim();
            var pin = (personalPin ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(pin))
            {
                TempData["AuthError"] = "Event code and PIN are required.";
                TempData["AuthMode"] = "join";
                return RedirectToAction("Login");
            }

            // 2) Look up the event by AccessCode (events.AccessCode in your DB)
            var ev = await _db.Events
                .FirstOrDefaultAsync(e => e.AccessCode == code);

            if (ev == null)
            {
                TempData["AuthError"] = "We couldn’t find an event with that code.";
                TempData["AuthMode"] = "join";
                return RedirectToAction("Login");
            }

            // Check if event is closed or archived
            if (ev.Status?.ToLower() == "closed" || ev.IsArchived)
            {
                TempData["AuthError"] = "This event has ended. The access code and PIN are now expired.";
                TempData["AuthMode"] = "join";
                return RedirectToAction("Login");
            }

            // 3) Route depending on ScoringLogic (WeightedAverage or PointBased)
            if (ev.ScoringLogic == "WeightedAverage" || ev.ScoringLogic == "PointBased")
            {
                var judge = await _db.Judges
                    .FirstOrDefaultAsync(j => j.EventId == ev.Id && j.Pin == pin);

                if (judge == null)
                {
                    TempData["AuthError"] = "Invalid PIN for this event.";
                    TempData["AuthMode"] = "join";
                    return RedirectToAction("Login");
                }

                // Store who is scoring in session
                HttpContext.Session.SetString("ScoringRole", "Judge");
                HttpContext.Session.SetString("ScoringName", judge.Name);
                HttpContext.Session.SetInt32("JudgeId", judge.Id);
                HttpContext.Session.SetInt32("EventId", ev.Id);

                // Audit log
                _db.AuditLogs.Add(new AuditLog
                {
                    EventId = ev.Id,
                    OrganizerId = null, // not an Organizer account
                    UserName = judge.Name,
                    UserRole = "Judge",
                    Action = "Judge Joined",
                    Details = $"Judge '{judge.Name}' joined scoring with PIN.",
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                // Redirect to Judge scoring screen
                return RedirectToAction("Index", "Judge", new { code = code, pin = pin });
            }

            // Unexpected event type
            TempData["AuthError"] = "This event cannot be joined.";
            TempData["AuthMode"] = "join";
            return RedirectToAction("Login");
        }

        // ============ PASSWORD STRENGTH HELPER ============
        private string? ValidatePasswordStrength(string password)
        {
            if (password.Length < 8)
                return "Password must be at least 8 characters long.";

            if (!password.Any(char.IsUpper))
                return "Password must contain at least one uppercase letter.";

            if (!password.Any(char.IsLower))
                return "Password must contain at least one lowercase letter.";

            if (!password.Any(char.IsDigit))
                return "Password must contain at least one number.";

            if (!password.Any(ch => "!@#$%^&*(),.?\":{}|<>_-".Contains(ch)))
                return "Password must contain at least one special character.";

            return null; // OK
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            // Audit Log for Judge/Scorer logout
            var role = HttpContext.Session.GetString("Role"); // "judge" or "scorer"
            var eventId = HttpContext.Session.GetInt32("EventId");
            string? name = null;

            if (role == "judge")
            {
                name = HttpContext.Session.GetString("JudgeName");
            }
            else if (role == "scorer")
            {
                name = HttpContext.Session.GetString("ScorerName");
            }
            // Fallback to existing legacy keys if any
            else
            {
                role = HttpContext.Session.GetString("ScoringRole");
                name = HttpContext.Session.GetString("ScoringName");
            }

            if (!string.IsNullOrEmpty(role) && !string.IsNullOrEmpty(name) && eventId.HasValue)
            {
                _db.AuditLogs.Add(new AuditLog
                {
                    EventId   = eventId.Value,
                    OrganizerId    = null,
                    UserName  = name,
                    UserRole  = role, // e.g. "judge"
                    Action    = "Logged Out",
                    Details   = $"{role} '{name}' logged out.",
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();

                // Notify Organizer
                await _notificationService.NotifyEventAsync(eventId.Value, "User Left", $"{role} '{name}' logged out.", "warning");
            }

            await ClearRememberMeAsync();
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // ============ HELPERS ============

        private async Task SetLoginSession(Organizer organizer)
        {
            // 1. Session (backward compatibility)
            HttpContext.Session.SetString("UserLoggedIn", "true");
            HttpContext.Session.SetString("UserEmail", organizer.Email);

            var displayName = (organizer.FirstName + " " + (organizer.LastName ?? "")).Trim();
            if (string.IsNullOrWhiteSpace(displayName)) displayName = organizer.Email;

            HttpContext.Session.SetString("UserName", displayName);
            HttpContext.Session.SetString("UserRole", "Organizer");
            HttpContext.Session.SetInt32("UserId", organizer.Id);

            // 2. Cookie Auth (Proper way for SignalR)
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, organizer.Id.ToString()),
                new Claim(ClaimTypes.Name, displayName),
                new Claim(ClaimTypes.Email, organizer.Email),
                new Claim(ClaimTypes.Role, "Organizer")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true, // We'll handle exact duration via options if needed
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12)
            });
        }

        private async Task HandleRememberMeAsync(Organizer organizer, bool rememberMe)
        {
            if (!rememberMe)
            {
                await ClearRememberMeAsync();
                return;
            }

            var rawToken = GenerateRandomToken();
            var hashed = HashToken(rawToken);
            organizer.RememberMeToken = hashed;
            organizer.RememberMeTokenExpiresAt = DateTime.UtcNow.AddDays(30);

            await _db.SaveChangesAsync();

            var cookieValue = $"{organizer.Id}|{rawToken}";
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = HttpContext.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30)
            };

            Response.Cookies.Append(RememberMeCookieName, cookieValue, options);
        }

        private async Task ClearRememberMeAsync()
        {
            if (Request.Cookies.ContainsKey(RememberMeCookieName))
            {
                Response.Cookies.Delete(RememberMeCookieName);
            }

            var userId = HttpContext.Session.GetInt32("UserId");
            if (userId.HasValue)
            {
                var organizer = await _db.Organizers.FindAsync(userId.Value);
                if (organizer != null)
                {
                    organizer.RememberMeToken = null;
                    organizer.RememberMeTokenExpiresAt = null;
                    await _db.SaveChangesAsync();
                }
            }
        }

        private async Task<Organizer?> TryAutoLoginFromCookieAsync()
        {
            if (!Request.Cookies.TryGetValue(RememberMeCookieName, out var cookieValue) ||
                string.IsNullOrWhiteSpace(cookieValue))
            {
                return null;
            }

            var parts = cookieValue.Split('|');
            if (parts.Length != 2) return null;

            if (!int.TryParse(parts[0], out var userId)) return null;
            var rawToken = parts[1];

            var organizer = await _db.Organizers.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (organizer == null ||
                string.IsNullOrWhiteSpace(organizer.RememberMeToken) ||
                !organizer.RememberMeTokenExpiresAt.HasValue ||
                organizer.RememberMeTokenExpiresAt.Value < DateTime.UtcNow)
            {
                await ClearRememberMeAsync();
                return null;
            }

            var hashed = HashToken(rawToken);
            if (!string.Equals(hashed, organizer.RememberMeToken, StringComparison.Ordinal))
            {
                await ClearRememberMeAsync();
                return null;
            }

            // Optionally rotate token each time – for now we just reuse it.
            return organizer;
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        private static bool VerifyPassword(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            var hashOfInput = HashPassword(password);
            return hashOfInput == storedHash;
        }

        private string GenerateEmailToken()
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("+", "")
                .Replace("/", "")
                .TrimEnd('=');
        }

        private string GenerateRandomToken()
        {
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[32];
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private string HashToken(string token)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(bytes);
        }
    }
}
