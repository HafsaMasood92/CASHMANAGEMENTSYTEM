using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class BillsController : Controller
    {
        private readonly AppDbContext _context;

        public BillsController(AppDbContext context)
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

        [HttpGet("/Bills")]
        [HttpGet("/Bills/Index")]
        public IActionResult Index()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                TempData["NoticeMessage"] = "Bills and payments are restricted to Organization Admins and authorized accountants.";
                return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = "Bills & Payments";
            ViewData["AllowedUnits"] = GetAllowedUnits();
            return View();
        }

        [HttpGet("/api/bills")]
        [HttpGet("/api/bills/data")]
        public async Task<IActionResult> GetBills()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Access denied. You do not have access to organization bills." });
            }

            var allowedUnits = GetAllowedUnits();
            var targetInstituteId = InstituteId ?? 1;
            var bills = await _context.Bills
                .Include(b => b.Payments)
                .Where(b => b.InstituteId == targetInstituteId && allowedUnits.Contains(b.UnitName))
                .OrderByDescending(b => b.DueDate)
                .ToListAsync();

            var today = DateTime.Today;

            // Recalculate status dynamically based on current date
            foreach (var b in bills)
            {
                var newStatus = b.ComputeStatus();
                if (b.Status != newStatus)
                {
                    b.Status = newStatus;
                }
            }
            await _context.SaveChangesAsync();

            var list = bills.Select(b => new
            {
                id = b.Id,
                title = b.Title,
                unit = b.UnitName,
                dueDate = b.DueDate.ToString("yyyy-MM-dd"),
                amount = b.TotalAmount,
                paid = b.PaidAmount,
                remaining = b.OutstandingAmount,
                status = b.Status,
                category = b.Category ?? "General"
            }).ToList();

            var totalBills = list.Count;
            var totalPaid = list.Sum(b => b.paid);
            var totalOutstanding = list.Sum(b => b.remaining);
            var overdueBills = list.Count(b => b.status == "Overdue");

            return Json(new { items = list, totalAmount = list.Sum(b => b.amount), totalBills, totalPaid, totalOutstanding, overdueBills });
        }

        [HttpPost("/api/bills")]
        [HttpPost("/api/bills/create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateBill([FromForm] string title, [FromForm] string unit, [FromForm] DateTime dueDate, [FromForm] decimal amount, [FromForm] decimal paid, [FromForm] string? category)
        {
            if (string.IsNullOrWhiteSpace(title) || amount <= 0 || string.IsNullOrWhiteSpace(unit))
            {
                return BadRequest(new { success = false, message = "Invalid bill details." });
            }

            var targetInstituteId = InstituteId ?? 1;
            var initialPaid = Math.Clamp(paid, 0, amount);
            var bill = new Bill
            {
                InstituteId = targetInstituteId,
                Title = title.Trim(),
                UnitName = unit.Trim(),
                DueDate = dueDate == default ? DateTime.Today.AddDays(30) : dueDate,
                TotalAmount = amount,
                PaidAmount = initialPaid,
                Category = string.IsNullOrWhiteSpace(category) ? "General Bill" : category.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            bill.Status = bill.ComputeStatus();

            _context.Bills.Add(bill);

            if (initialPaid > 0)
            {
                bill.Payments.Add(new BillPayment
                {
                    Amount = initialPaid,
                    PaymentDate = DateTime.Today,
                    PaymentMethod = "Bank Account",
                    Notes = "Initial payment upon bill creation",
                    RecordedBy = HttpContext.Session.GetString("UserName") ?? "Accountant",
                    CreatedAt = DateTime.UtcNow
                });

                // Record corresponding expense in transactions
                _context.Transactions.Add(new Transaction
                {
                    InstituteId = targetInstituteId,
                    Description = $"Payment for Bill: {bill.Title}",
                    Amount = initialPaid,
                    Type = "Expense",
                    TransactionDate = DateTime.Today,
                    UnitName = bill.UnitName,
                    PaymentMethod = "Bank Account",
                    Category = "Bill Payment",
                    ReferenceNumber = $"BILL-PMT-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
                    CreatedBy = HttpContext.Session.GetString("UserName") ?? "Accountant",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Check if overdue to create notification (avoiding duplicate unread notifications)
            if (bill.Status == "Overdue")
            {
                var duplicateBillNotif = await _context.Notifications.AnyAsync(n =>
                    n.InstituteId == targetInstituteId &&
                    n.Type == "Bill" &&
                    n.Title.Contains(bill.Title) &&
                    !n.IsRead);

                if (!duplicateBillNotif)
                {
                    _context.Notifications.Add(new NotificationItem
                    {
                        InstituteId = targetInstituteId,
                        Type = "Bill",
                        Title = $"Overdue Bill Added: {bill.Title}",
                        Message = $"Bill for {bill.UnitName} of Rs. {bill.OutstandingAmount:N0} is overdue.",
                        UnitName = bill.UnitName,
                        IsActionRequired = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, billId = bill.Id });
        }

        [HttpPost("/api/bills/record-payment")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RecordPayment([FromForm] int billId, [FromForm] decimal paymentAmount, [FromForm] string? paymentMethod)
        {
            var bill = await _context.Bills.Include(b => b.Payments).FirstOrDefaultAsync(b => b.Id == billId);
            if (bill == null)
            {
                return NotFound(new { success = false, message = "Bill not found." });
            }

            var remaining = bill.OutstandingAmount;
            if (paymentAmount <= 0 || paymentAmount > remaining)
            {
                return BadRequest(new { success = false, message = "Payment amount must be greater than zero and no more than remaining balance." });
            }

            var method = string.IsNullOrWhiteSpace(paymentMethod) ? "Bank Account" : paymentMethod.Trim();
            var payment = new BillPayment
            {
                BillId = bill.Id,
                Amount = paymentAmount,
                PaymentDate = DateTime.Today,
                PaymentMethod = method,
                Notes = $"Recorded bill payment of Rs. {paymentAmount:N0}",
                RecordedBy = HttpContext.Session.GetString("UserName") ?? "Accountant",
                CreatedAt = DateTime.UtcNow
            };

            _context.BillPayments.Add(payment);
            bill.PaidAmount += paymentAmount;
            bill.Status = bill.ComputeStatus();

            // Record as Expense transaction in financial ledger
            var targetInstituteId = bill.InstituteId ?? (InstituteId ?? 1);
            _context.Transactions.Add(new Transaction
            {
                InstituteId = targetInstituteId,
                Description = $"Bill payment for: {bill.Title}",
                Amount = paymentAmount,
                Type = "Expense",
                TransactionDate = DateTime.Today,
                UnitName = bill.UnitName,
                PaymentMethod = method,
                Category = "Bill Payment",
                ReferenceNumber = $"BILL-PMT-{DateTime.Now:yyyyMMdd}-{Random.Shared.Next(100, 999)}",
                CreatedBy = HttpContext.Session.GetString("UserName") ?? "Accountant",
                CreatedAt = DateTime.UtcNow
            });

            // Notification
            _context.Notifications.Add(new NotificationItem
            {
                InstituteId = targetInstituteId,
                Type = "Bill",
                Title = $"Payment recorded for {bill.Title}",
                Message = $"Rs. {paymentAmount:N0} paid. Remaining balance is Rs. {bill.OutstandingAmount:N0}.",
                UnitName = bill.UnitName,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Json(new { success = true, paid = bill.PaidAmount, remaining = bill.OutstandingAmount, status = bill.Status });
        }
    }
}
