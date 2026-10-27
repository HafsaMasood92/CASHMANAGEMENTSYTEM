using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
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
            var allowedUnits = GetAllowedUnits();
            ViewData["Title"] = "Overview";
            ViewData["AllowedUnits"] = allowedUnits;

            if (UserRole == "Super Admin")
            {
                ViewData["OverdueCount"] = 0;
                ViewData["DeletionCount"] = 0;
            }
            else
            {
                var targetInstituteId = InstituteId ?? -1;
                // Counts for Needs Attention
                var overdueBillsCount = await _context.Bills
                    .Where(b => b.InstituteId == targetInstituteId && allowedUnits.Contains(b.UnitName) && b.Status == "Overdue")
                    .CountAsync();

                var deletionCount = await _context.Notifications
                    .Where(n => n.InstituteId == targetInstituteId && (n.Type == "Deletion" || n.Type == "Approval") && !n.IsRead)
                    .CountAsync();

                ViewData["OverdueCount"] = overdueBillsCount;
                ViewData["DeletionCount"] = deletionCount;
            }

            return View();
        }

        [HttpGet("/api/dashboard/data")]
        public async Task<IActionResult> GetDashboardData(string period = "month", string unit = "all")
        {
            var isSuperAdmin = UserRole == "Super Admin";

            if (isSuperAdmin)
            {
                var totalOrganizations = await _context.Institutes.CountAsync();
                var totalOwners = await _context.Users.CountAsync(u => u.Role == "Organization Owner" || u.Role == "Organization Admin");
                var totalUsers = await _context.Users.CountAsync();

                var organizations = await _context.Institutes
                    .Include(i => i.Units)
                    .OrderByDescending(i => i.CreatedAt)
                    .Select(i => new
                    {
                        id = i.Id,
                        name = i.Name,
                        createdAt = i.CreatedAt.ToString("dd MMM yyyy"),
                        unitCount = i.Units.Count,
                        units = i.Units.Select(u => u.Name).ToList(),
                        owner = _context.Users
                            .Where(u => u.InstituteId == i.Id && (u.Role == "Organization Owner" || u.Role == "Organization Admin"))
                            .Select(u => new { name = u.FullName, email = u.Email })
                            .FirstOrDefault(),
                        userCount = _context.Users.Count(u => u.InstituteId == i.Id)
                    })
                    .ToListAsync();

                var orgList = organizations.Select(i => new
                {
                    id = i.id,
                    name = i.name,
                    createdAt = i.createdAt,
                    unitCount = i.unitCount,
                    units = i.units,
                    ownerName = i.owner != null ? i.owner.name : "Unassigned",
                    ownerEmail = i.owner != null ? i.owner.email : "N/A",
                    userCount = i.userCount
                }).ToList();

                return Json(new
                {
                    isSuperAdmin = true,
                    totalOrganizations,
                    totalOwners,
                    totalUsers,
                    organizations = orgList
                });
            }

            if (UserRole == "User")
            {
                return Json(new
                {
                    periodRevenue = 0,
                    periodExpenses = 0,
                    allTimeNet = 0,
                    outstandingBills = 0,
                    overdueCount = 0,
                    deletionCount = 0,
                    unitPerformance = new List<object>(),
                    monthlyFlow = new List<object>(),
                    recentTransactions = new List<object>()
                });
            }

            var allowedUnits = GetAllowedUnits();
            var targetUnits = unit == "all"
                ? allowedUnits
                : (allowedUnits.Contains(unit, StringComparer.OrdinalIgnoreCase) ? new List<string> { unit } : allowedUnits);

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

            var targetInstituteId = InstituteId ?? (isSuperAdmin ? 1 : -1);

            // Query transactions for authorized units only
            var query = _context.Transactions
                .Where(t => t.InstituteId == targetInstituteId && targetUnits.Contains(t.UnitName));

            if (startDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate >= startDate.Value);
            }
            if (endDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate <= endDate.Value);
            }

            var transactions = await query.ToListAsync();

            var periodRevenue = transactions
                .Where(t => t.Type == "Revenue" && t.Category != "Account Transfer")
                .Sum(t => t.Amount);

            var periodExpenses = transactions
                .Where(t => t.Type == "Expense" && t.Category != "Account Transfer")
                .Sum(t => t.Amount);

            // All-time transactions for Net Balance
            var allTimeTransactions = await _context.Transactions
                .Where(t => t.InstituteId == targetInstituteId && targetUnits.Contains(t.UnitName))
                .ToListAsync();

            var openingBalances = await _context.BankAccounts
                .Where(b => b.IsActive && b.InstituteId == targetInstituteId)
                .SumAsync(b => b.OpeningBalance);

            var allTimeRevenue = allTimeTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var allTimeExpenses = allTimeTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var allTimeNet = openingBalances + allTimeRevenue - allTimeExpenses;

            // Outstanding bills
            var bills = await _context.Bills
                .Where(b => b.InstituteId == targetInstituteId && targetUnits.Contains(b.UnitName))
                .ToListAsync();

            var outstandingBills = bills.Sum(b => b.OutstandingAmount);
            var overdueCount = bills.Count(b => b.ComputeStatus() == "Overdue");

            var deletionCount = await _context.Notifications
                .Where(n => n.InstituteId == targetInstituteId && (n.Type == "Deletion" || n.Type == "Approval") && !n.IsRead)
                .CountAsync();

            // Monthly cash flow trend for the last 6 months
            var monthlyFlow = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var monthStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var monthEnd = monthStart.AddMonths(1).AddDays(-1);

                var monthTxs = allTimeTransactions
                    .Where(t => t.TransactionDate >= monthStart && t.TransactionDate <= monthEnd && t.Category != "Account Transfer")
                    .ToList();

                var mRev = monthTxs.Where(t => t.Type == "Revenue").Sum(t => t.Amount);
                var mExp = monthTxs.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                monthlyFlow.Add(new
                {
                    month = monthStart.ToString("MMM"),
                    revenue = mRev,
                    expenses = mExp
                });
            }

            // Unit performance breakdown
            var unitPerformance = new List<object>();
            foreach (var u in targetUnits)
            {
                var uTxs = transactions.Where(t => t.UnitName == u && t.Category != "Account Transfer").ToList();
                var uRev = uTxs.Where(t => t.Type == "Revenue").Sum(t => t.Amount);
                var uExp = uTxs.Where(t => t.Type == "Expense").Sum(t => t.Amount);

                unitPerformance.Add(new
                {
                    id = u.ToLower().Replace(" ", ""),
                    name = u,
                    revenue = uRev,
                    expenses = uExp,
                    netBalance = uRev - uExp
                });
            }

            // Recent transactions for owner / accountant
            var recentTransactions = transactions
                .Where(t => t.Category != "Account Transfer")
                .OrderByDescending(t => t.TransactionDate)
                .ThenByDescending(t => t.Id)
                .Take(6)
                .Select(t => new
                {
                    id = t.Id,
                    description = t.Description,
                    unit = t.UnitName,
                    amount = t.Amount,
                    type = t.Type,
                    date = t.TransactionDate.ToString("dd MMM yyyy"),
                    account = t.PaymentMethod
                })
                .ToList();

            // Action required notifications
            var actionNotifications = await _context.Notifications
                .Where(n => n.InstituteId == targetInstituteId && n.IsActionRequired && !n.IsRead && (string.IsNullOrEmpty(n.UnitName) || targetUnits.Contains(n.UnitName)))
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new
                {
                    id = n.Id,
                    title = n.Title,
                    message = n.Message,
                    unit = n.UnitName ?? "All Units",
                    type = n.Type,
                    time = n.CreatedAt.ToString("dd MMM yyyy")
                })
                .ToListAsync();

            return Json(new
            {
                isSuperAdmin = false,
                totalCash = allTimeNet,
                unitSummaries = unitPerformance,
                periodRevenue,
                periodExpenses,
                allTimeNet,
                outstandingBills,
                overdueCount,
                deletionCount,
                monthlyFlow,
                unitPerformance,
                recentTransactions,
                actionNotifications
            });
        }
    }
}
