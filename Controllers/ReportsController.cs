
using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class ReportsController : Controller
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
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

        public async Task<IActionResult> Index()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                TempData["NoticeMessage"] = "Financial reports are reserved for Organization Admins and Accountants.";
                return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = "Financial Reports";
            var allowedUnits = GetAllowedUnits();
            var units = await _context.OrganizationalUnits
                .Where(u => u.IsActive && allowedUnits.Contains(u.Name))
                .Select(u => u.Name)
                .ToListAsync();

            ViewBag.AvailableUnits = units;
            return View();
        }

        [HttpGet("/api/reports/data")]
        public async Task<IActionResult> GetReportData([FromQuery] string period = "month", [FromQuery] string unit = "all")
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                return StatusCode(403, new { success = false, message = "Access denied. Financial reports are reserved for Organization Admins and Accountants." });
            }

            var allowedUnits = GetAllowedUnits();
            var now = DateTime.Today;

            DateTime? startDate = period switch
            {
                "month" => new DateTime(now.Year, now.Month, 1),
                "lastMonth" => new DateTime(now.Year, now.Month, 1).AddMonths(-1),
                "year" => new DateTime(now.Year, 1, 1),
                _ => null
            };

            DateTime? endDate = period switch
            {
                "lastMonth" => new DateTime(now.Year, now.Month, 1).AddDays(-1),
                _ => null
            };

            var targetInstId = InstituteId ?? 1;

            // Query transactions
            var txQuery = _context.Transactions
                .Where(t => t.InstituteId == targetInstId && allowedUnits.Contains(t.UnitName));

            if (!string.IsNullOrWhiteSpace(unit) && unit != "all")
            {
                txQuery = txQuery.Where(t => t.UnitName == unit);
            }

            var allTransactions = await txQuery.ToListAsync();

            var periodTxQuery = allTransactions.AsEnumerable();
            if (startDate.HasValue) periodTxQuery = periodTxQuery.Where(t => t.TransactionDate >= startDate.Value);
            if (endDate.HasValue) periodTxQuery = periodTxQuery.Where(t => t.TransactionDate <= endDate.Value);
            var periodTransactions = periodTxQuery.ToList();

            var periodRevenue = periodTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var periodExpenses = periodTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount);

            var allTimeRevenue = allTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var allTimeExpenses = allTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount);

            var openingBalanceTotal = (unit == "all") 
                ? await _context.BankAccounts.Where(b => b.IsActive && b.InstituteId == targetInstId).SumAsync(b => b.OpeningBalance)
                : 0m;

            var allTimeNet = openingBalanceTotal + allTimeRevenue - allTimeExpenses;

            // Bills
            var billsQuery = _context.Bills.Where(b => b.InstituteId == targetInstId && allowedUnits.Contains(b.UnitName));
            if (!string.IsNullOrWhiteSpace(unit) && unit != "all")
            {
                billsQuery = billsQuery.Where(b => b.UnitName == unit);
            }
            var allBills = await billsQuery.ToListAsync();
            var totalOutstanding = allBills.Sum(b => b.OutstandingAmount);

            // Monthly data for current year (Jan - Dec)
            var currentYear = now.Year;
            var months = new List<object>();
            string[] monthNames = { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            for (int m = 1; m <= 12; m++)
            {
                var mStart = new DateTime(currentYear, m, 1);
                var mEnd = mStart.AddMonths(1).AddDays(-1);

                var mTx = allTransactions.Where(t => t.TransactionDate >= mStart && t.TransactionDate <= mEnd).ToList();
                months.Add(new
                {
                    month = monthNames[m - 1],
                    revenue = mTx.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount),
                    expenses = mTx.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount)
                });
            }

            // Organizational unit breakdown
            var unitsToBreakdown = (unit != "all" ? new List<string> { unit } : allowedUnits);
            var unitBreakdown = new List<object>();

            foreach (var u in unitsToBreakdown)
            {
                var uTxs = allTransactions.Where(t => t.UnitName == u).ToList();
                var uRev = uTxs.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount);
                var uExp = uTxs.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount);
                var uOut = allBills.Where(b => b.UnitName == u).Sum(b => b.OutstandingAmount);

                unitBreakdown.Add(new
                {
                    unit = u,
                    revenue = uRev,
                    expenses = uExp,
                    net = uRev - uExp,
                    outstanding = uOut
                });
            }

            // Period transactions for export
            var exportList = periodTransactions.OrderByDescending(t => t.TransactionDate).Take(100).Select(t => new
            {
                date = t.TransactionDate.ToString("yyyy-MM-dd"),
                unit = t.UnitName,
                type = t.Type,
                description = t.Description,
                amount = t.Amount
            }).ToList();

            return Json(new
            {
                success = true,
                totalRevenue = periodRevenue,
                totalExpense = periodExpenses,
                unitFilter = unit,
                periodFilter = period,
                periodRevenue,
                periodExpenses,
                allTimeNet,
                totalOutstanding,
                months,
                unitBreakdown,
                transactions = exportList
            });
        }
    }
}
