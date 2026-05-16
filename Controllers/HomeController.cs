using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectTallify.Models;
using System.Linq;
using System.Threading.Tasks;

namespace ProjectTallify.Controllers
{
    public class HomeController : Controller
    {
        private readonly TallifyDbContext _db;

        public HomeController(TallifyDbContext db)
        {
            _db = db;
        }

        // GET: / or /Home
        public IActionResult Index()
        {
            // Send root to Dashboard
            return RedirectToAction("Dashboard");
        }

        // GET: /Home/Dashboard
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Dashboard";
            ViewBag.ActiveNav = "Dashboard";

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            // Only OPEN events, not archived, and belonging to the user
            var activeEvents = await _db.Events
                .Where(e => e.UserId == userId.Value && e.Status == "open" && !e.IsArchived)
                .OrderBy(e => e.StartDateTime)
                .ToListAsync();

            // Pass active events as model to Dashboard.cshtml
            return View(activeEvents);
        }

        // GET: /Home/CreateEvent
        // Just redirect to the real Create Event wizard in EventsController
        public IActionResult CreateEvent()
        {
            return RedirectToAction("Create", "Events");
        }

        // GET: /Home/Events  (for your header nav)
        public IActionResult Events()
        {
            return RedirectToAction("Index", "Events");
        }

        // GET: /Home/AuditLogs
        public async Task<IActionResult> AuditLogs(string? search, string sort = "desc")
        {
            ViewData["Title"] = "Audit Logs";
            ViewBag.ActiveNav = "AuditLogs";
            ViewBag.CurrentSearch = search;
            ViewBag.CurrentSort = sort;

            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue) return RedirectToAction("Login", "Auth");

            var query = _db.AuditLogs
                .Include(a => a.User)
                .Include(a => a.Event)
                .Where(a => 
                    (a.UserId == userId.Value) || // User's own account actions
                    (a.Event != null && a.Event.UserId == userId.Value) // Actions on user's events
                )
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLower();
                query = query.Where(a => 
                    (a.UserName != null && a.UserName.ToLower().Contains(s)) ||
                    (a.Action != null && a.Action.ToLower().Contains(s)) ||
                    (a.Event != null && a.Event.Name.ToLower().Contains(s)) ||
                    (a.Details != null && a.Details.ToLower().Contains(s))
                );
            }

            if (sort == "asc")
            {
                query = query.OrderBy(a => a.CreatedAt);
            }
            else
            {
                query = query.OrderByDescending(a => a.CreatedAt);
            }

            var logs = await query.ToListAsync();

            return View(logs);
        }
    }
}
