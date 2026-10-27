using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        private string? UserEmail => HttpContext.Session.GetString("UserEmail");
        private string UserRole => HttpContext.Session.GetString("UserRole") ?? "Super Admin";
        private int? InstituteId => HttpContext.Session.GetInt32("InstituteId");

        private List<string> GetAllowedUnits()
        {
            var json = HttpContext.Session.GetString("AssignedUnits");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<List<string>>(json);
                    if (parsed != null && parsed.Any()) return parsed;
                }
                catch { }
            }

            if (UserRole == "Super Admin")
            {
                return _context.OrganizationalUnits
                    .Where(u => u.IsActive)
                    .Select(u => u.Name)
                    .ToList();
            }

            if (InstituteId.HasValue)
            {
                var instUnits = _context.InstituteUnits
                    .Where(u => u.InstituteId == InstituteId.Value)
                    .Select(u => u.Name)
                    .ToList();
                if (instUnits.Any()) return instUnits;
            }

            return new List<string>();
        }

        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewData["Title"] = "Notifications";
            return View();
        }

        [HttpGet("/api/notifications")]
        public async Task<IActionResult> GetNotifications(string? search, string type = "all", string status = "all")
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return Unauthorized(new { success = false, message = "Not authenticated." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = InstituteId ?? (isSuperAdmin ? 1 : -1);
            var allowedUnits = GetAllowedUnits();

            var query = _context.Notifications.AsQueryable();

            if (isSuperAdmin)
            {
                // Super Admin receives notifications ONLY when an Organization Admin/Owner account is created
                // and when Super Admin logs in. General financial, bill, expense, or organization activity notifications are excluded.
                query = query.Where(n => n.InstituteId == null &&
                    (n.Title.Contains("Organization Admin") || n.Title.Contains("Organization Owner") || n.Title.Contains("Super Admin Login")));
            }
            else
            {
                // Organization Admin receives notifications for their own organization's activities only
                query = query.Where(n => n.InstituteId == targetInstituteId &&
                    (string.IsNullOrEmpty(n.UnitName) || n.UnitName == "All Units" || allowedUnits.Contains(n.UnitName)));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(n => n.Title.ToLower().Contains(term) || n.Message.ToLower().Contains(term) || n.UnitName.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(type) && type != "all")
            {
                query = query.Where(n => n.Type == type);
            }

            if (status == "unread")
            {
                query = query.Where(n => !n.IsRead);
            }
            else if (status == "read")
            {
                query = query.Where(n => n.IsRead);
            }
            else if (status == "action")
            {
                query = query.Where(n => n.IsActionRequired);
            }

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new
                {
                    id = n.Id,
                    type = n.Type,
                    title = n.Title,
                    message = n.Message,
                    unit = n.UnitName,
                    time = FormatRelativeTime(n.CreatedAt),
                    unread = !n.IsRead,
                    actionRequired = n.IsActionRequired
                })
                .ToListAsync();

            // Total statistics
            var statsQuery = _context.Notifications.AsQueryable();
            if (isSuperAdmin)
            {
                statsQuery = statsQuery.Where(n => n.InstituteId == null &&
                    (n.Title.Contains("Organization Admin") || n.Title.Contains("Organization Owner") || n.Title.Contains("Super Admin Login")));
            }
            else
            {
                statsQuery = statsQuery.Where(n => n.InstituteId == targetInstituteId &&
                    (string.IsNullOrEmpty(n.UnitName) || n.UnitName == "All Units" || allowedUnits.Contains(n.UnitName)));
            }
            var allUserNotifications = await statsQuery.ToListAsync();

            var totalCount = allUserNotifications.Count;
            var unreadCount = allUserNotifications.Count(n => !n.IsRead);
            var actionCount = allUserNotifications.Count(n => n.IsActionRequired && !n.IsRead);

            return Json(new { success = true, items, notifications = items, totalCount, unreadCount, actionCount });
        }

        [HttpPost("/api/notifications/mark-read")]
        public async Task<IActionResult> MarkRead([FromBody] NotificationIdModel model)
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return Unauthorized(new { success = false, message = "Not authenticated." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = InstituteId ?? (isSuperAdmin ? 1 : -1);

            var notification = await _context.Notifications.FindAsync(model.Id);
            if (notification == null)
            {
                return NotFound(new { success = false, message = "Notification not found." });
            }

            if (isSuperAdmin)
            {
                if (notification.InstituteId != null || (!notification.Title.Contains("Organization Admin") && !notification.Title.Contains("Organization Owner") && !notification.Title.Contains("Super Admin Login")))
                {
                    return NotFound(new { success = false, message = "Notification not found." });
                }
            }
            else
            {
                if (notification.InstituteId != targetInstituteId)
                {
                    return NotFound(new { success = false, message = "Notification not found." });
                }
            }

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost("/api/notifications/mark-all-read")]
        public async Task<IActionResult> MarkAllRead()
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return Unauthorized(new { success = false, message = "Not authenticated." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = InstituteId ?? (isSuperAdmin ? 1 : -1);
            var allowedUnits = GetAllowedUnits();

            var unreadQuery = _context.Notifications.Where(n => !n.IsRead);
            if (isSuperAdmin)
            {
                unreadQuery = unreadQuery.Where(n => n.InstituteId == null &&
                    (n.Title.Contains("Organization Admin") || n.Title.Contains("Organization Owner") || n.Title.Contains("Super Admin Login")));
            }
            else
            {
                unreadQuery = unreadQuery.Where(n => n.InstituteId == targetInstituteId &&
                    (string.IsNullOrEmpty(n.UnitName) || n.UnitName == "All Units" || allowedUnits.Contains(n.UnitName)));
            }

            var unreadList = await unreadQuery.ToListAsync();

            foreach (var n in unreadList)
            {
                n.IsRead = true;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, markedCount = unreadList.Count });
        }

        [HttpPost("/api/notifications/approve-deletion")]
        public async Task<IActionResult> ApproveDeletion([FromBody] NotificationIdModel model)
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return Unauthorized(new { success = false, message = "Not authenticated." });
            }

            if (UserRole != "Super Admin" && UserRole != "Organization Owner" && UserRole != "Organization Admin")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only Administrators can approve transaction deletions." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = InstituteId ?? (isSuperAdmin ? 1 : -1);

            var notification = await _context.Notifications.FindAsync(model.Id);
            if (notification == null || (!isSuperAdmin && notification.InstituteId != targetInstituteId))
            {
                return NotFound(new { success = false, message = "Notification not found." });
            }

            var match = System.Text.RegularExpressions.Regex.Match(notification.Title, @"#(\d+)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var txId))
            {
                var tx = await _context.Transactions.FindAsync(txId);
                if (tx != null && (isSuperAdmin || tx.InstituteId == targetInstituteId))
                {
                    _context.Transactions.Remove(tx);

                    _context.Notifications.Add(new NotificationItem
                    {
                        InstituteId = targetInstituteId,
                        Type = "Audit",
                        Title = $"Transaction #{txId} Deletion Approved",
                        Message = $"{UserRole} approved deletion of transaction '{tx.Description}' (Rs. {tx.Amount:N0}).",
                        UnitName = tx.UnitName,
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            notification.IsRead = true;
            notification.IsActionRequired = false;
            notification.Message += $" [APPROVED by {UserRole}]";

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Transaction deletion approved and completed." });
        }

        [HttpPost("/api/notifications/reject-deletion")]
        public async Task<IActionResult> RejectDeletion([FromBody] NotificationIdModel model)
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return Unauthorized(new { success = false, message = "Not authenticated." });
            }

            if (UserRole != "Super Admin" && UserRole != "Organization Owner" && UserRole != "Organization Admin")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Only Super Admin or Organization Admins can reject requests." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = InstituteId ?? (isSuperAdmin ? 1 : -1);

            var notification = await _context.Notifications.FindAsync(model.Id);
            if (notification == null || (!isSuperAdmin && notification.InstituteId != targetInstituteId))
            {
                return NotFound(new { success = false, message = "Notification not found." });
            }

            notification.IsRead = true;
            notification.IsActionRequired = false;
            notification.Message += $" [REJECTED by {UserRole}]";

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Request rejected." });
        }

        private static string FormatRelativeTime(DateTime date)
        {
            var span = DateTime.UtcNow - date;
            if (span.TotalMinutes < 2) return "Just now";
            if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
            if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
            if (span.TotalDays < 7) return $"{(int)span.TotalDays}d ago";
            return date.ToString("dd MMM yyyy");
        }
    }

    public class NotificationIdModel
    {
        public int Id { get; set; }
    }
}
