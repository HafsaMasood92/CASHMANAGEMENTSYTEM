using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class TransactionsController : Controller
    {
        private readonly AppDbContext _context;

        public TransactionsController(AppDbContext context)
        {
            _context = context;
        }

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
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                TempData["NoticeMessage"] = "Organization financial ledgers are restricted to Organization Admins and authorized accountants.";
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction(nameof(Revenue));
        }

        public async Task<IActionResult> Revenue()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                TempData["NoticeMessage"] = "Organization financial ledgers are restricted to Organization Admins and authorized accountants.";
                return RedirectToAction("Index", "Home");
            }
            ViewData["Title"] = "Revenue";
            ViewData["AllowedUnits"] = GetAllowedUnits();
            return View();
        }

        public async Task<IActionResult> Expenses()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                TempData["NoticeMessage"] = "Organization financial ledgers are restricted to Organization Admins and authorized accountants.";
                return RedirectToAction("Index", "Home");
            }
            ViewData["Title"] = "Expenses";
            ViewData["AllowedUnits"] = GetAllowedUnits();
            return View();
        }

        // --- JSON APIs for Revenue ---

        [HttpGet("/api/transactions/revenue")]
        public async Task<IActionResult> GetRevenueTransactions()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Access denied. You do not have access to private financial transactions." });
            }

            var allowedUnits = GetAllowedUnits();
            var targetInstituteId = InstituteId ?? 1;
            var list = await _context.Transactions
                .Where(t => t.Type == "Revenue" && t.InstituteId == targetInstituteId && allowedUnits.Contains(t.UnitName))
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .Select(t => new
                {
                    id = t.Id,
                    description = t.Description,
                    date = t.TransactionDate.ToString("yyyy-MM-dd"),
                    amount = t.Amount,
                    unit = t.UnitName,
                    account = t.PaymentMethod,
                    category = t.Category,
                    refNumber = t.ReferenceNumber ?? ""
                })
                .ToListAsync();

            var total = list.Sum(t => t.amount);
            var count = list.Count;
            var average = count > 0 ? total / count : 0;

            return Json(new { items = list, total, count, average });
        }

        [HttpPost("/api/transactions/revenue")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddRevenue([FromForm] string description, [FromForm] DateTime date, [FromForm] decimal amount, [FromForm] string unit, [FromForm] string account, [FromForm] string? category)
        {
            if (string.IsNullOrWhiteSpace(description) || amount <= 0 || string.IsNullOrWhiteSpace(unit))
            {
                return BadRequest(new { success = false, message = "Invalid transaction data. Description and positive amount are required." });
            }

            var allowedUnits = GetAllowedUnits();
            if (!allowedUnits.Contains(unit.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = $"Access denied. You do not have permission to post revenue for unit '{unit}'." });
            }

            var targetInstituteId = InstituteId ?? 1;
            var transaction = new Transaction
            {
                Description = description.Trim(),
                Amount = amount,
                Type = "Revenue",
                TransactionDate = date == default ? DateTime.Today : date,
                UnitName = unit.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(account) ? "Cash" : account.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Tuition Fees" : category.Trim(),
                ReferenceNumber = $"REC-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
                CreatedBy = HttpContext.Session.GetString("UserName") ?? "Accountant",
                CreatedAt = DateTime.UtcNow,
                InstituteId = targetInstituteId
            };

            _context.Transactions.Add(transaction);

            // Notify
            _context.Notifications.Add(new NotificationItem
            {
                InstituteId = targetInstituteId,
                Type = "Transaction",
                Title = $"New Revenue recorded: {transaction.UnitName}",
                Message = $"{transaction.Description} (Rs. {transaction.Amount:N0}) was posted into {transaction.PaymentMethod}.",
                UnitName = transaction.UnitName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, transactionId = transaction.Id });
        }

        // --- JSON APIs for Expenses ---

        [HttpGet("/api/transactions/expenses")]
        public async Task<IActionResult> GetExpenseTransactions()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Access denied. You do not have access to private financial transactions." });
            }

            var allowedUnits = GetAllowedUnits();
            var targetInstituteId = InstituteId ?? 1;
            var list = await _context.Transactions
                .Where(t => t.Type == "Expense" && t.InstituteId == targetInstituteId && allowedUnits.Contains(t.UnitName))
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .Select(t => new
                {
                    id = t.Id,
                    description = t.Description,
                    date = t.TransactionDate.ToString("yyyy-MM-dd"),
                    amount = t.Amount,
                    unit = t.UnitName,
                    account = t.PaymentMethod,
                    category = t.Category,
                    refNumber = t.ReferenceNumber ?? ""
                })
                .ToListAsync();

            var total = list.Sum(t => t.amount);
            var count = list.Count;
            var average = count > 0 ? total / count : 0;

            return Json(new { items = list, total, count, average });
        }

        [HttpPost("/api/transactions/expenses")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddExpense([FromForm] string description, [FromForm] DateTime date, [FromForm] decimal amount, [FromForm] string unit, [FromForm] string account, [FromForm] string? category)
        {
            if (string.IsNullOrWhiteSpace(description) || amount <= 0 || string.IsNullOrWhiteSpace(unit))
            {
                return BadRequest(new { success = false, message = "Invalid expense data. Description and positive amount are required." });
            }

            var allowedUnits = GetAllowedUnits();
            if (!allowedUnits.Contains(unit.Trim(), StringComparer.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = $"Access denied. You do not have permission to post expenses for unit '{unit}'." });
            }

            var targetInstituteId = InstituteId ?? 1;
            var transaction = new Transaction
            {
                Description = description.Trim(),
                Amount = amount,
                Type = "Expense",
                TransactionDate = date == default ? DateTime.Today : date,
                UnitName = unit.Trim(),
                PaymentMethod = string.IsNullOrWhiteSpace(account) ? "Cash" : account.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "Operational Cost" : category.Trim(),
                ReferenceNumber = $"EXP-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
                CreatedBy = HttpContext.Session.GetString("UserName") ?? "Accountant",
                CreatedAt = DateTime.UtcNow,
                InstituteId = targetInstituteId
            };

            _context.Transactions.Add(transaction);

            // Notify
            _context.Notifications.Add(new NotificationItem
            {
                InstituteId = targetInstituteId,
                Type = "Transaction",
                Title = $"New Expense recorded: {transaction.UnitName}",
                Message = $"{transaction.Description} (Rs. {transaction.Amount:N0}) was paid from {transaction.PaymentMethod}.",
                UnitName = transaction.UnitName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, transactionId = transaction.Id });
        }

        // --- Deletion & Request-Approval Flows ---

        [HttpPost("/api/transactions/delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteTransaction(int id)
        {
            var role = UserRole;
            // Only Organization Admin or Owner can directly delete financial transactions
            if (role != "Super Admin" && role != "Organization Owner" && role != "Organization Admin")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "Accountants cannot directly delete financial records. Please submit a deletion request for Admin approval."
                });
            }

            var tx = await _context.Transactions.FindAsync(id);
            if (tx == null)
            {
                return NotFound(new { success = false, message = "Transaction not found." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = tx.InstituteId ?? (InstituteId ?? (isSuperAdmin ? 1 : -1));
            if (!isSuperAdmin && tx.InstituteId != targetInstituteId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Access denied." });
            }

            _context.Transactions.Remove(tx);

            // Audit notification
            var userName = HttpContext.Session.GetString("UserName") ?? role;
            _context.Notifications.Add(new NotificationItem
            {
                InstituteId = targetInstituteId,
                Type = "Audit",
                Title = $"Transaction #{id} Deleted",
                Message = $"{role} {userName} deleted transaction '{tx.Description}' (Rs. {tx.Amount:N0}).",
                UnitName = tx.UnitName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = $"Transaction #{id} successfully deleted." });
        }

        [HttpPost("/api/transactions/request-deletion")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDeletion([FromForm] int transactionId, [FromForm] string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                return BadRequest(new { success = false, message = "A reason must be provided for the deletion request." });
            }

            var tx = await _context.Transactions.FindAsync(transactionId);
            if (tx == null)
            {
                return NotFound(new { success = false, message = "Transaction not found." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var targetInstituteId = tx.InstituteId ?? (InstituteId ?? (isSuperAdmin ? 1 : -1));
            if (!isSuperAdmin && tx.InstituteId != targetInstituteId)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Access denied." });
            }

            var userName = HttpContext.Session.GetString("UserName") ?? "Accountant";
            var role = UserRole;

            var pendingDuplicate = await _context.Notifications.AnyAsync(n =>
                n.InstituteId == targetInstituteId &&
                n.Type == "Approval" &&
                n.Title.Contains($"Tx #{transactionId}") &&
                n.IsActionRequired &&
                !n.IsRead);

            if (pendingDuplicate)
            {
                return BadRequest(new { success = false, message = $"A deletion approval request for Transaction #{transactionId} is already pending." });
            }

            _context.Notifications.Add(new NotificationItem
            {
                InstituteId = targetInstituteId,
                Type = "Approval",
                Title = $"Deletion Approval Request: Tx #{transactionId}",
                Message = $"{role} {userName} requested deletion of transaction '{tx.Description}' (Rs. {tx.Amount:N0}, {tx.UnitName}). Reason: {reason.Trim()}",
                UnitName = tx.UnitName,
                IsActionRequired = true,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Deletion request for Transaction #{transactionId} has been submitted to Organization Owners for approval."
            });
        }
    }
}
