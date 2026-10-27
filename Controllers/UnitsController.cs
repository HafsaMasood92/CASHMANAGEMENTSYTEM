using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class UnitsController : Controller
    {
        private readonly AppDbContext _context;

        public UnitsController(AppDbContext context)
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
            ViewData["Title"] = "Organizational Units";
            return View();
        }

        [HttpGet("/api/units")]
        [HttpGet("/api/units/data")]
        public async Task<IActionResult> GetUnits()
        {
            var isSuperAdmin = UserRole == "Super Admin";
            var allowedUnits = GetAllowedUnits();

            // Auto-sync allowedUnits to OrganizationalUnits table if missing
            bool changed = false;
            foreach (var unitName in allowedUnits)
            {
                if (!await _context.OrganizationalUnits.AnyAsync(u => u.Name.ToLower() == unitName.ToLower()))
                {
                    _context.OrganizationalUnits.Add(new OrganizationalUnit
                    {
                        Name = unitName,
                        Category = "Organization Unit",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                    changed = true;
                }
            }
            if (changed)
            {
                await _context.SaveChangesAsync();
            }

            var query = _context.OrganizationalUnits.AsQueryable();

            if (!isSuperAdmin)
            {
                // RESTRICT: Only the units assigned access by the user!
                query = query.Where(u => allowedUnits.Contains(u.Name));
            }

            var units = await query
                .OrderBy(u => u.Id)
                .Select(u => new
                {
                    id = u.Id,
                    name = u.Name,
                    category = u.Category,
                    active = u.IsActive,
                    isAllowed = true
                })
                .ToListAsync();

            var totalUnits = units.Count;
            var activeUnits = units.Count(u => u.active);
            var inactiveUnits = totalUnits - activeUnits;

            return Json(new { items = units, totalUnits, activeUnits, inactiveUnits });
        }

        [HttpPost("/api/units")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateUnit([FromForm] string name, [FromForm] string category)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { success = false, message = "Unit name is required." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var isOrgAdmin = UserRole == "Organization Admin" || UserRole == "Organization Owner";
            if (!isSuperAdmin && !isOrgAdmin)
            {
                return StatusCode(403, new { success = false, message = "Access denied. Only administrators can create units." });
            }

            var trimmedName = name.Trim();
            var exists = await _context.OrganizationalUnits.AnyAsync(u => u.Name.ToLower() == trimmedName.ToLower());
            if (exists)
            {
                return BadRequest(new { success = false, message = "A unit with this name already exists." });
            }

            var unit = new OrganizationalUnit
            {
                Name = trimmedName,
                Category = string.IsNullOrWhiteSpace(category) ? "Other" : category.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.OrganizationalUnits.Add(unit);

            // If created by Org Admin, attach to their institute
            var targetInstId = InstituteId ?? 1;
            var institute = await _context.Institutes.Include(i => i.Units).FirstOrDefaultAsync(i => i.Id == targetInstId);
            if (institute != null && !institute.Units.Any(u => u.Name.Equals(trimmedName, StringComparison.OrdinalIgnoreCase)))
            {
                institute.Units.Add(new InstituteUnit { Name = trimmedName });
            }

            await _context.SaveChangesAsync();

            if (!isSuperAdmin)
            {
                var allowed = GetAllowedUnits();
                if (!allowed.Contains(trimmedName, StringComparer.OrdinalIgnoreCase))
                {
                    allowed.Add(trimmedName);
                    HttpContext.Session.SetString("AssignedUnits", JsonSerializer.Serialize(allowed));

                    var userEmail = HttpContext.Session.GetString("UserEmail");
                    if (!string.IsNullOrEmpty(userEmail))
                    {
                        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == userEmail.ToLower());
                        if (user != null)
                        {
                            user.AssignedUnitsJson = JsonSerializer.Serialize(allowed);
                            await _context.SaveChangesAsync();
                        }
                    }
                }
            }

            return Json(new { success = true, unitId = unit.Id });
        }

        [HttpPost("/api/units/toggle-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleUnitStatus([FromForm] int id)
        {
            var unit = await _context.OrganizationalUnits.FindAsync(id);
            if (unit == null)
            {
                return NotFound(new { success = false, message = "Unit not found." });
            }

            var isSuperAdmin = UserRole == "Super Admin";
            var allowedUnits = GetAllowedUnits();

            if (!isSuperAdmin && !allowedUnits.Contains(unit.Name, StringComparer.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { success = false, message = "Access denied. You cannot modify units outside your assigned access." });
            }

            unit.IsActive = !unit.IsActive;
            await _context.SaveChangesAsync();

            return Json(new { success = true, active = unit.IsActive });
        }

        [HttpGet("/Units/Dashboard/{id:int?}")]
        [HttpGet("/Units/Dashboard")]
        public async Task<IActionResult> Dashboard(string? name, int? unitId = null, int? id = null)
        {
            if (id.HasValue && !unitId.HasValue)
            {
                unitId = id;
            }

            var allowedUnits = GetAllowedUnits();
            var isSuperAdmin = UserRole == "Super Admin";

            if (string.IsNullOrWhiteSpace(name) && unitId.HasValue)
            {
                var found = await _context.OrganizationalUnits.FindAsync(unitId.Value);
                if (found != null) name = found.Name;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                if (!isSuperAdmin && !allowedUnits.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = $"Access denied. You do not have permission to access the '{name}' dashboard.";
                    return RedirectToAction("Index", "Units");
                }
            }
            else
            {
                name = allowedUnits.FirstOrDefault();
                if (string.IsNullOrEmpty(name))
                {
                    TempData["ErrorMessage"] = "No organizational units are assigned to your account.";
                    return RedirectToAction("Index", "Units");
                }
            }

            var unit = await _context.OrganizationalUnits
                .FirstOrDefaultAsync(u => u.Name.ToLower() == name.ToLower());

            var unitName = unit?.Name ?? name;

            ViewData["Title"] = unitName + " Dashboard";
            ViewData["UnitName"] = unitName;
            ViewData["AllowedUnits"] = allowedUnits;

            return View();
        }

        [HttpGet("/api/units/dashboard-data")]
        public async Task<IActionResult> GetUnitDashboardData(string? name, int? unitId = null, string period = "month", int? instituteId = null)
        {
            if (string.IsNullOrWhiteSpace(name) && unitId.HasValue)
            {
                var found = await _context.OrganizationalUnits.FindAsync(unitId.Value);
                if (found != null) name = found.Name;
            }

            var allowedUnits = GetAllowedUnits();
            var isSuperAdmin = UserRole == "Super Admin";

            if (string.IsNullOrWhiteSpace(name))
            {
                name = allowedUnits.FirstOrDefault();
                if (string.IsNullOrEmpty(name))
                {
                    return StatusCode(403, new { success = false, message = "No permitted units found for this account." });
                }
            }

            if (!isSuperAdmin && !allowedUnits.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return StatusCode(403, new { success = false, message = $"Access denied. You do not have permission to view metrics for '{name}'." });
            }

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

            var targetInstituteId = (isSuperAdmin && instituteId.HasValue) 
                ? instituteId.Value 
                : (InstituteId ?? (isSuperAdmin ? 1 : -1));

            // Query transactions for this unit and organization
            var allUnitTransactions = await _context.Transactions
                .Where(t => t.UnitName == name && t.InstituteId == targetInstituteId)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            var periodQuery = allUnitTransactions.AsEnumerable();
            if (startDate.HasValue) periodQuery = periodQuery.Where(t => t.TransactionDate >= startDate.Value);
            if (endDate.HasValue) periodQuery = periodQuery.Where(t => t.TransactionDate <= endDate.Value);

            var periodTransactions = periodQuery.ToList();

            var periodRevenue = periodTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var periodExpenses = periodTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount);

            var allTimeRevenue = allUnitTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var allTimeExpenses = allUnitTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount);
            var allTimeNet = allTimeRevenue - allTimeExpenses;

            // Outstanding bills for this unit and organization
            var bills = await _context.Bills
                .Where(b => b.UnitName == name && b.InstituteId == targetInstituteId)
                .ToListAsync();
            var outstandingBills = bills.Sum(b => b.OutstandingAmount);

            // Cash on hand vs Bank balance for this unit
            var cashRevenue = allUnitTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer" && t.PaymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            var cashExpenses = allUnitTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer" && t.PaymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            var cashOnHand = Math.Max(0, cashRevenue - cashExpenses);

            var bankRevenue = allUnitTransactions.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer" && !t.PaymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            var bankExpenses = allUnitTransactions.Where(t => t.Type == "Expense" && t.Category != "Account Transfer" && !t.PaymentMethod.Contains("Cash", StringComparison.OrdinalIgnoreCase)).Sum(t => t.Amount);
            var bankBalance = Math.Max(0, bankRevenue - bankExpenses);

            // Monthly 6-month trend for this unit
            var months = new List<object>();
            for (int i = 5; i >= 0; i--)
            {
                var mStart = new DateTime(now.Year, now.Month, 1).AddMonths(-i);
                var mEnd = mStart.AddMonths(1).AddDays(-1);

                var mTxs = allUnitTransactions.Where(t => t.TransactionDate >= mStart && t.TransactionDate <= mEnd).ToList();
                months.Add(new
                {
                    label = mStart.ToString("MMM"),
                    revenue = mTxs.Where(t => t.Type == "Revenue" && t.Category != "Account Transfer").Sum(t => t.Amount),
                    expenses = mTxs.Where(t => t.Type == "Expense" && t.Category != "Account Transfer").Sum(t => t.Amount)
                });
            }

            // Recent activities
            var recentActivities = allUnitTransactions.Take(6).Select(t => new
            {
                description = t.Description,
                type = t.Type,
                date = t.TransactionDate.ToString("dd MMM yyyy"),
                account = t.PaymentMethod,
                amount = t.Amount
            }).ToList();

            return Json(new
            {
                success = true,
                unitName = name,
                periodRevenue,
                periodExpenses,
                allTimeNet,
                outstandingBills,
                cashOnHand,
                bankBalance,
                months,
                recentActivities
            });
        }
    }
}
