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
                    SetLoginSession(autoUser);
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

            // Look up user by email (regardless of active status initially)
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.Email == email);

            // 1) Email not found at all
            if (user == null)
            {
                TempData["AuthError"] = "No account found for this email. Please sign up first.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            // 2) Check if account is deactivated - MOVED AFTER PASSWORD CHECK
            // We want to verify password first so we don't leak account status or allow reactivation without proof of ownership.

            // 3) Email exists but not verified yet
            if (!user.EmailConfirmed)
            {
                // ... (existing email confirmation logic) ...
                if (user.EmailVerificationTokenExpiresAt.HasValue &&
                    user.EmailVerificationTokenExpiresAt.Value < DateTime.UtcNow)
                {
                    user.EmailVerificationToken = GenerateEmailToken();
                    user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                    await _db.SaveChangesAsync();

                    var confirmationLink = Url.Action(
                        "ConfirmEmail",
                        "Auth",
                        new { userId = user.Id, token = user.EmailVerificationToken },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailConfirmationAsync(user, confirmationLink!);

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
            if (!VerifyPassword(password, user.HashedPassword))
            {
                TempData["AuthError"] = "Incorrect email or password.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }
            
            // NOW check Deactivation status
            if (!user.IsActive)
            {
                // Password is correct, but account is inactive.
                // Prompt for reactivation.
                ViewBag.ShowReactivationModal = true;
                ViewBag.ReactivationUserId = user.Id;
                ViewBag.AuthMode = "login"; // Ensure we stay on login tab
                
                // We need to return View directly to pass ViewBag (Redirect kills it)
                // Re-populate standard view bags
                ViewData["Title"] = "Login";
                ViewBag.HideOrgCard = true;
                ViewBag.IsAuthPage = true;
                return View("Login");
            }

            // 5) Success – set session + optionally remember-me cookie, go to Dashboard
            SetLoginSession(user);
            await HandleRememberMeAsync(user, rememberMe);

            return RedirectToAction("Dashboard", "Home");
        }

        // ============ REACTIVATE ACCOUNT (POST) ============
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateAccount(int userId)
        {
            var user = await _db.Users.FindAsync(userId);
            if (user == null)
            {
                TempData["AuthError"] = "User not found.";
                return RedirectToAction("Login");
            }

            // Reactivate
            user.IsActive = true;
            
            // Audit Log
            _db.AuditLogs.Add(new AuditLog
            {
                EventId = null,
                UserId = user.Id,
                UserName = user.Email,
                UserRole = user.Role,
                Action = "Account Reactivated",
                Details = $"User '{user.Email}' reactivated their account.",
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            // Log them in immediately
            SetLoginSession(user);

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
            var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

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

            var user = new User
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                HashedPassword = HashPassword(password),
                EmailConfirmed = false,
                IsActive = true,
                Role = "Organizer",
                EmailVerificationToken = GenerateEmailToken(),
                EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(15)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var confirmationLink = Url.Action(
                "ConfirmEmail",
                "Auth",
                new { userId = user.Id, token = user.EmailVerificationToken },
                protocol: Request.Scheme);

            await _emailSender.SendEmailConfirmationAsync(user, confirmationLink!);

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
            var user = await _db.Users.FindAsync(userId);

            if (user == null ||
                string.IsNullOrWhiteSpace(user.EmailVerificationToken) ||
                !string.Equals(user.EmailVerificationToken, token, StringComparison.Ordinal))
            {
                TempData["AuthError"] = "Invalid verification link.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            // Expired → automatically send a new one
            if (user.EmailVerificationTokenExpiresAt.HasValue &&
                user.EmailVerificationTokenExpiresAt.Value < DateTime.UtcNow)
            {
                user.EmailVerificationToken = GenerateEmailToken();
                user.EmailVerificationTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                await _db.SaveChangesAsync();

                var newLink = Url.Action(
                    "ConfirmEmail",
                    "Auth",
                    new { userId = user.Id, token = user.EmailVerificationToken },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailConfirmationAsync(user, newLink!);

                TempData["AuthError"] =
                    "This verification link has expired. A new verification email has been sent. " +
                    "Please verify within 15 minutes.";
                TempData["AuthMode"] = "login";
                return RedirectToAction("Login");
            }

            user.EmailConfirmed = true;
            user.EmailVerificationToken = null;
            user.EmailVerificationTokenExpiresAt = null;
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

            // Look for active, confirmed user with this email
            var user = await _db.Users
                .FirstOrDefaultAsync(u => u.IsActive && u.Email == email && u.EmailConfirmed);

            if (user != null)
            {
                user.PasswordResetToken = GenerateRandomToken();
                user.PasswordResetTokenExpiresAt = DateTime.UtcNow.AddMinutes(15);
                await _db.SaveChangesAsync();

                var resetLink = Url.Action(
                    "ResetPassword",
                    "Auth",
                    new { userId = user.Id, token = user.PasswordResetToken },
                    protocol: Request.Scheme);

                await _emailSender.SendPasswordResetAsync(user, resetLink!);
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
            var user = await _db.Users.FindAsync(userId);

            if (user == null ||
                string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
                !string.Equals(user.PasswordResetToken, token, StringComparison.Ordinal) ||
                !user.PasswordResetTokenExpiresAt.HasValue ||
                user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
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
            var user = await _db.Users.FindAsync(userId);

            // 1. Validate that the link is still valid
            if (user == null ||
                string.IsNullOrWhiteSpace(user.PasswordResetToken) ||
                !string.Equals(user.PasswordResetToken, token, StringComparison.Ordinal) ||
                !user.PasswordResetTokenExpiresAt.HasValue ||
                user.PasswordResetTokenExpiresAt.Value < DateTime.UtcNow)
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
            user.HashedPassword = HashPassword(password);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiresAt = null;
            user.RememberMeToken = null;
            user.RememberMeTokenExpiresAt = null;

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

            // 3) Route depending on EventType (events.EventType in your DB)
            //    "criteria"  -> Judges table
            //    "orw"       -> Scorers table
            if (ev.EventType == "criteria")
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
                    UserId = null, // not an Organizer account
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
                    UserId    = null,
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

        private void SetLoginSession(User user)
        {
            HttpContext.Session.SetString("UserLoggedIn", "true");
            HttpContext.Session.SetString("UserEmail", user.Email);

            var displayName = (user.FirstName + " " + (user.LastName ?? "")).Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = user.Email;   // fallback to email
            }

            HttpContext.Session.SetString("UserName", displayName);
            HttpContext.Session.SetString("UserRole", user.Role ?? "Organizer");
            HttpContext.Session.SetInt32("UserId", user.Id);
        }

        private async Task HandleRememberMeAsync(User user, bool rememberMe)
        {
            if (!rememberMe)
            {
                await ClearRememberMeAsync();
                return;
            }

            var rawToken = GenerateRandomToken();
            var hashed = HashToken(rawToken);
            user.RememberMeToken = hashed;
            user.RememberMeTokenExpiresAt = DateTime.UtcNow.AddDays(30);

            await _db.SaveChangesAsync();

            var cookieValue = $"{user.Id}|{rawToken}";
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
                var user = await _db.Users.FindAsync(userId.Value);
                if (user != null)
                {
                    user.RememberMeToken = null;
                    user.RememberMeTokenExpiresAt = null;
                    await _db.SaveChangesAsync();
                }
            }
        }

        private async Task<User?> TryAutoLoginFromCookieAsync()
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

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
            if (user == null ||
                string.IsNullOrWhiteSpace(user.RememberMeToken) ||
                !user.RememberMeTokenExpiresAt.HasValue ||
                user.RememberMeTokenExpiresAt.Value < DateTime.UtcNow)
            {
                await ClearRememberMeAsync();
                return null;
            }

            var hashed = HashToken(rawToken);
            if (!string.Equals(hashed, user.RememberMeToken, StringComparison.Ordinal))
            {
                await ClearRememberMeAsync();
                return null;
            }

            // Optionally rotate token each time – for now we just reuse it.
            return user;
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
