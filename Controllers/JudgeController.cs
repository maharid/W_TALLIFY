using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json;
using ProjectTallify.Services;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ProjectTallify.Controllers
{
    /// <summary>
    /// Controller for the Judge's interface.
    /// Handles login, scoring interface rendering, and score submission.
    /// </summary>
    public class JudgeController : Controller
    {
        private readonly TallifyDbContext _db;
        private readonly IScoringService _scoringService;
        private readonly INotificationService _notificationService;

        public JudgeController(TallifyDbContext db, IScoringService scoringService, INotificationService notificationService)
        {
            _db = db;
            _scoringService = scoringService;
            _notificationService = notificationService;
        }

        // ============================================================
        // LOGIN PAGE / API
        // ============================================================
        
        /// <summary>
        /// Handles login for both Judges and Scorers (ORW).
        /// Validates Access Code and PIN.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.AccessCode) || string.IsNullOrWhiteSpace(request.Pin))
            {
                return BadRequest(new { success = false, message = "Access Code and PIN are required." });
            }

            var trimmedCode = request.AccessCode.Trim();
            var trimmedPin = request.Pin.Trim();

            // 1. Find Event
            var ev = await _db.Events.FirstOrDefaultAsync(e => e.AccessCode.ToLower() == trimmedCode.ToLower());
            if (ev == null)
            {
                return NotFound(new { success = false, message = "Event not found." });
            }

            // 2. Try finding a JUDGE
            var judge = await _db.Judges
                .FirstOrDefaultAsync(j => j.EventId == ev.Id && j.Pin == trimmedPin);

            if (judge != null)
            {
                if (!judge.IsActive)
                    return Unauthorized(new { success = false, message = "This judge account is inactive." });

                // Success: Judge
                // 1. Session (backward compatibility)
                HttpContext.Session.SetInt32("UserId", judge.Id); 
                HttpContext.Session.SetInt32("JudgeId", judge.Id);
                HttpContext.Session.SetInt32("EventId", ev.Id);
                HttpContext.Session.SetString("UserName", judge.Name);
                HttpContext.Session.SetString("JudgeName", judge.Name);
                HttpContext.Session.SetString("Role", "judge");

                // 2. Cookie Auth (Proper way for SignalR)
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, $"Judge_{judge.Id}"),
                    new Claim(ClaimTypes.Name, judge.Name),
                    new Claim(ClaimTypes.Role, "Judge"),
                    new Claim("EventId", ev.Id.ToString())
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                _db.AuditLogs.Add(new AuditLog
                {
                    EventId = ev.Id,
                    OrganizerId = null,
                    UserName = judge.Name,
                    UserRole = "Judge",
                    Action = "Entered Judging Room",
                    Details = $"Judge '{judge.Name}' entered the judging room.",
                    CreatedAt = DateTime.UtcNow
                });
                await _db.SaveChangesAsync();
                
                await _notificationService.NotifyEventAsync(ev.Id, "Judge Joined", $"{judge.Name} has entered the judging room.", "info");

                return Ok(new 
                {  
                    success = true, 
                    message = "Login successful",
                    role = "judge",
                    redirectUrl = Url.Action("Index", "Judge", new { code = ev.AccessCode }) 
                });
            }

            // 3. Neither found
            return Unauthorized(new { success = false, message = "Invalid PIN for this event." });
        }

        // ============================================================
        // MAIN JUDGE SCORING SCREEN
        // ============================================================
        
        /// <summary>
        /// Renders the main scoring interface for a Judge.
        /// Automatically detects the active round or uses the provided roundId.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Index(string? code, int? roundId)
        {
            // 1. Verify Session or Code
            var sessionEventId = HttpContext.Session.GetInt32("EventId");
            var sessionRole    = HttpContext.Session.GetString("Role");
            
            Event? ev = null;

            var query = _db.Events
                .Include(e => e.Contestants)
                .Include(e => e.Rounds)
                .ThenInclude(r => r.Criterias)
                .Include(e => e.Organizer) 
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(code))
            {
                ev = await query.FirstOrDefaultAsync(e => e.AccessCode == code);
            }
            else if (sessionEventId.HasValue)
            {
                ev = await query.FirstOrDefaultAsync(e => e.Id == sessionEventId.Value);
            }

            if (ev == null) return NotFound("Event not found.");

            ViewBag.OrganizationName = ev.Organizer.OrganizationName ?? "The Organization";
            ViewBag.OrganizationSubtitle = ev.Organizer.OrganizationSubtitle ?? "Subtitle";
            ViewBag.OrganizationPhotoPath = ev.Organizer.OrganizationPhotoPath;

            if (sessionEventId.HasValue && sessionEventId.Value == ev.Id)
            {
                if (sessionRole != "judge")
                {
                    // If they are logged in but not as a judge for this event, we might want to handle it.
                }
            }

            ViewData["Title"] = "Judge – Scoring";
            ViewBag.HideHeader  = true;
            ViewBag.HideOrgCard = true;
            ViewBag.ThemeColor = ev.ThemeColor;
            ViewBag.OrgColor = ev.Organizer.ThemeColor;

            Round? activeRound = null;
            var allRounds = ev.Rounds.OrderBy(r => r.Order).ToList();
            
            var ongoingRound = allRounds.FirstOrDefault(r => r.IsActive);

            if (ongoingRound != null)
            {
                activeRound = ongoingRound;
            }
            else
            {
                if (roundId.HasValue)
                {
                    activeRound = allRounds.FirstOrDefault(r => r.Id == roundId.Value);
                }

                if (activeRound == null)
                {
                    activeRound = allRounds
                        .FirstOrDefault();
                }
            }

            var roundName = activeRound?.Name ?? "Round 1";

            var contestants = ev.Contestants
                .Where(c => c.IsActive) 
                .OrderBy(c => c.Code)
                .ToList();

            var contestantVms = contestants
                .Select(c => new JudgeContestantViewModel
                {
                    ContestantId = c.Code,
                    FullName     = c.Name,
                    Subtitle     = c.Organization ?? "",
                    PhotoPath    = c.PhotoPath
                })
                .ToList();

            var selectedId = contestantVms.FirstOrDefault()?.ContestantId;

            var criteriaRounds = new List<Round>();
            if (activeRound != null)
            {
                criteriaRounds.Add(activeRound);
            }

            var groups = criteriaRounds
                .Select(r => new CriteriaGroupViewModel
                {
                    GroupName = r.Name,
                    Criteria = r.Criterias
                        .Where(c => c.MinPoints != -1 && c.MaxPoints != -1) 
                        .OrderBy(c => c.DisplayOrder ?? int.MaxValue)
                        .Select(c => new CriteriaRowViewModel
                        {
                            Name              = c.Name,
                            WeightPercentage  = c.WeightPercent,
                            MinPoints         = c.MinPoints,
                            MaxPoints         = c.MaxPoints,
                            Score             = null 
                        })
                        .ToList()
                })
                .ToList();

            bool isSubmitted = false;
            var judgeId = HttpContext.Session.GetInt32("JudgeId");
            if (judgeId.HasValue && activeRound != null)
            {
                isSubmitted = await _db.Scores.AnyAsync(s => s.EventId == ev.Id && s.RoundId == activeRound.Id && s.JudgeId == judgeId.Value);
            }

            var vm = new JudgeViewModel
            {
                EventTitle          = ev.Name,
                EventCode           = ev.AccessCode ?? "",
                RoundName           = roundName,
                RoundId             = activeRound?.Id ?? 0,
                IsRoundActive       = (ev.Status?.ToLower() == "open") && (activeRound?.IsActive ?? false),
                IsSubmitted         = isSubmitted,
                EventStatus         = ev.Status?.ToLower() ?? "preparing",
                Schedule            = ev.Schedule,
                Venue               = ev.Venue,
                SelectedContestantId = selectedId,
                ThemeColor          = ev.ThemeColor,
                HeaderImage         = ev.HeaderImage,
                JudgeName           = HttpContext.Session.GetString("JudgeName") ?? "Judge",
                OrganizationName    = ev.Organizer.OrganizationName ?? "Organization", 
                OrganizationSubtitle = ev.Organizer.OrganizationSubtitle ?? "Subtitle",
                OrganizationPhotoPath = ev.Organizer.OrganizationPhotoPath,
                Contestants         = contestantVms,
                Groups              = groups
            };

            return View("Index", vm);
        }

        // ============================================================
        // SUBMIT SCORES (AJAX)
        // ============================================================
        
        /// <summary>
        /// Handles bulk submission of scores from the Judge's interface.
        /// Processes the scores via ScoringService and notifies the system.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit([FromBody] JsonElement payload)
        {
            try 
            {
                string eventCode = payload.TryGetProperty("eventCode", out var ec) ? ec.GetString() ?? "" : "";
                
                if (string.IsNullOrEmpty(eventCode))
                    return BadRequest(new { success = false, message = "Missing event code." });

                var ev = await _db.Events.FirstOrDefaultAsync(e => e.AccessCode == eventCode);
                if (ev == null)
                    return NotFound(new { success = false, message = "Event not found." });

                var judgeId = HttpContext.Session.GetInt32("JudgeId");
                
                // FALLBACK: If session is null, try to get JudgeId from Claims
                if (judgeId == null && User.Identity.IsAuthenticated)
                {
                    var claim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    if (claim != null && claim.StartsWith("Judge_"))
                    {
                        if (int.TryParse(claim.Replace("Judge_", ""), out int parsedId))
                        {
                            judgeId = parsedId;
                            // Restore session for subsequent calls
                            HttpContext.Session.SetInt32("JudgeId", judgeId.Value);
                            
                            var judge = await _db.Judges.FindAsync(judgeId.Value);
                            if (judge != null)
                            {
                                HttpContext.Session.SetString("JudgeName", judge.Name);
                                HttpContext.Session.SetInt32("EventId", judge.EventId);
                            }
                        }
                    }
                }

                if (judgeId == null)
                {
                     return Unauthorized(new { success = false, message = "Session expired. Please login again." });
                }

                int? roundId = payload.TryGetProperty("roundId", out var rid) ? rid.GetInt32() : (int?)null;
                
                List<ContestantScoreSubmission>? submissions = null;
                if (payload.TryGetProperty("submissions", out var subsElement) && subsElement.ValueKind == JsonValueKind.Array)
                {
                    submissions = JsonSerializer.Deserialize<List<ContestantScoreSubmission>>(subsElement.GetRawText(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }

                if (!roundId.HasValue || submissions == null || !submissions.Any())
                {
                    return BadRequest(new { success = false, message = "Invalid submission data." });
                }

                await _scoringService.ProcessJudgeRoundSubmission(ev.Id, judgeId.Value, roundId.Value, submissions);
                
                var judgeName = HttpContext.Session.GetString("JudgeName") ?? "Judge";
                
                _db.AuditLogs.Add(new AuditLog
                {
                    EventId = ev.Id,
                    OrganizerId = null, 
                    UserName = judgeName,
                    UserRole = "Judge",
                    Action = "Round Scores Submitted",
                    Details = $"Judge '{judgeName}' submitted all scores for round {roundId.Value}.",
                    CreatedAt = DateTime.UtcNow
                });
                
                await _db.SaveChangesAsync();

                // Trigger Real-time Tally Refresh for Organizers
                await _notificationService.NotifyScoresUpdatedAsync(ev.Id);

                var totalJudges = await _db.Judges.CountAsync(j => j.EventId == ev.Id);
                
                var submittedJudges = await _db.Scores
                    .Where(s => s.EventId == ev.Id && s.RoundId == roundId.Value)
                    .Select(s => s.JudgeId)
                    .Distinct()
                    .CountAsync();

                return Ok(new { success = true, message = "All scores submitted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = "Server error processing scores: " + ex.Message });
            }
        }
    }

    public class LoginRequest
    {
        public string? AccessCode { get; set; }
        public string? Pin { get; set; }
    }
}