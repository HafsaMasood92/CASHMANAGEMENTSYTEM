using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class SettingsController : Controller
    {
        private readonly AppDbContext _context;

        public SettingsController(AppDbContext context)
        {
            _context = context;
        }

        private string? UserEmail => HttpContext.Session.GetString("UserEmail");
        private string UserRole => HttpContext.Session.GetString("UserRole") ?? "";
        private int? UserInstituteId => HttpContext.Session.GetInt32("InstituteId");

        private bool IsSuperAdmin() => UserRole == "Super Admin";
        private bool IsOrgAdmin() => UserRole == "Organization Admin" || UserRole == "Organization Owner";

        private bool IsAuthorizedAdminOrOwner()
        {
            return !string.IsNullOrEmpty(UserEmail) && (IsSuperAdmin() || IsOrgAdmin());
        }

        [HttpGet("/Settings")]
        [HttpGet("/Settings/Index")]
        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            if (IsSuperAdmin())
            {
                return RedirectToAction("Index", "Users");
            }

            if (!IsOrgAdmin())
            {
                TempData["ErrorMessage"] = "Access denied. Settings are reserved for Organization Admins.";
                return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = "Settings";
            return View();
        }

        [HttpGet("/api/settings/data")]
        public async Task<IActionResult> GetSettingsData()
        {
            if (!IsOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var targetInstituteId = UserInstituteId ?? 1;

            var institute = await _context.Institutes.FindAsync(targetInstituteId);
            var organizationName = institute?.Name ?? "Organization";
            var officialEmail = "admin@organization.com";

            var adminUser = await _context.Users.FirstOrDefaultAsync(u => u.InstituteId == targetInstituteId && (u.Role == "Organization Admin" || u.Role == "Organization Owner"));
            if (adminUser != null) officialEmail = adminUser.Email;

            // Categories: only organization-specific categories for Organization Admins
            var categories = await _context.FinancialCategories
                .Where(c => c.InstituteId == targetInstituteId)
                .OrderBy(c => c.Id)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    type = c.Type.ToLower(),
                    active = c.IsActive
                })
                .ToListAsync();

            var settingsList = await _context.SystemSettings.ToListAsync();
            var settingsDict = settingsList.ToDictionary(s => s.SettingKey, s => s.SettingValue);

            // Override with actual organization profile
            settingsDict["OrganizationName"] = organizationName;
            if (!settingsDict.ContainsKey("OfficialEmail") || string.IsNullOrWhiteSpace(settingsDict["OfficialEmail"]))
            {
                settingsDict["OfficialEmail"] = officialEmail;
            }

            return Json(new
            {
                success = true,
                isSuperAdmin = false,
                organizationName,
                categories = categories.Cast<object>().ToList(),
                settings = settingsDict
            });
        }

        [HttpGet("/api/categories")]
        public async Task<IActionResult> GetCategoriesForDropdown([FromQuery] string? type = null)
        {
            var targetInstituteId = UserInstituteId ?? 1;
            var query = _context.FinancialCategories
                .Where(c => c.InstituteId == targetInstituteId && c.IsActive);

            if (!string.IsNullOrWhiteSpace(type))
            {
                var norm = type.Trim().ToLower();
                query = query.Where(c => c.Type.ToLower() == norm);
            }

            var list = await query
                .OrderBy(c => c.Name)
                .Select(c => new { id = c.Id, name = c.Name, type = c.Type.ToLower() })
                .ToListAsync();

            return Json(new { success = true, items = list, categories = list });
        }

        [HttpPost("/api/settings/category/add")]
        public async Task<IActionResult> AddCategory()
        {
            if (!IsOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied. Only Organization Admins can manage organization categories." });
            }

            string? name = null;
            string? type = null;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                name = form["name"].ToString();
                type = form["type"].ToString();
            }
            else
            {
                try
                {
                    var model = await System.Text.Json.JsonSerializer.DeserializeAsync<CategoryCreateModel>(Request.Body, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    name = model?.Name;
                    type = model?.Type;
                }
                catch { }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { success = false, message = "Category name is required." });
            }

            var targetInstituteId = UserInstituteId ?? 1;
            var categoryType = type?.Trim() ?? "Revenue";
            var normalizedType = categoryType.Equals("expense", StringComparison.OrdinalIgnoreCase) ? "Expense"
                               : categoryType.Equals("bill", StringComparison.OrdinalIgnoreCase) ? "Bill"
                               : "Revenue";

            var trimmedName = name.Trim();
            var exists = await _context.FinancialCategories
                .AnyAsync(c => c.InstituteId == targetInstituteId && c.Name.ToLower() == trimmedName.ToLower() && c.Type == normalizedType);

            if (exists)
            {
                return BadRequest(new { success = false, message = "This category already exists in your organization." });
            }

            var category = new FinancialCategory
            {
                Name = trimmedName,
                Type = normalizedType,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                InstituteId = targetInstituteId
            };

            _context.FinancialCategories.Add(category);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = category.Id, message = $"Category '{trimmedName}' created." });
        }

        [HttpPost("/api/settings/category/edit")]
        public async Task<IActionResult> EditCategory([FromBody] CategoryEditModel model)
        {
            if (!IsOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied. Only Organization Admins can edit categories." });
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { success = false, message = "Category name cannot be empty." });
            }

            var targetInstituteId = UserInstituteId ?? 1;
            var category = await _context.FinancialCategories
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.InstituteId == targetInstituteId);

            if (category == null)
            {
                return NotFound(new { success = false, message = "Category not found." });
            }

            var oldName = category.Name;
            var newName = model.Name.Trim();

            // Check if another category with same name exists
            var exists = await _context.FinancialCategories
                .AnyAsync(c => c.InstituteId == targetInstituteId && c.Id != model.Id && c.Name.ToLower() == newName.ToLower() && c.Type == category.Type);
            if (exists)
            {
                return BadRequest(new { success = false, message = "A category with this name already exists." });
            }

            category.Name = newName;

            // Propagate name change to transactions and bills so data stays linked
            var txs = await _context.Transactions.Where(t => t.Category == oldName).ToListAsync();
            foreach (var t in txs) t.Category = newName;

            var bills = await _context.Bills.Where(b => b.Category == oldName).ToListAsync();
            foreach (var b in bills) b.Category = newName;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Category updated to '{newName}'." });
        }

        [HttpPost("/api/settings/category/delete")]
        public async Task<IActionResult> DeleteCategory([FromBody] CategoryToggleModel model)
        {
            if (!IsOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied. Only Organization Admins can delete categories." });
            }

            var targetInstituteId = UserInstituteId ?? 1;
            var category = await _context.FinancialCategories
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.InstituteId == targetInstituteId);

            if (category == null)
            {
                return NotFound(new { success = false, message = "Category not found." });
            }

            // CRITICAL RULE: Prevent deletion of categories already used in financial records!
            var isUsedInTx = await _context.Transactions.AnyAsync(t => t.InstituteId == targetInstituteId && t.Category == category.Name);
            var isUsedInBills = await _context.Bills.AnyAsync(b => b.InstituteId == targetInstituteId && b.Category == category.Name);

            if (isUsedInTx || isUsedInBills)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Cannot delete category '{category.Name}'. Active financial transactions or bills are recorded under this category. You may deactivate it instead to preserve financial records."
                });
            }

            _context.FinancialCategories.Remove(category);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Category '{category.Name}' deleted successfully." });
        }

        [HttpPost("/api/settings/category/toggle")]
        public async Task<IActionResult> ToggleCategory([FromBody] CategoryToggleModel model)
        {
            if (!IsOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var targetInstituteId = UserInstituteId ?? 1;
            var category = await _context.FinancialCategories
                .FirstOrDefaultAsync(c => c.Id == model.Id && c.InstituteId == targetInstituteId);

            if (category == null)
            {
                return NotFound(new { success = false, message = "Category not found." });
            }

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();

            return Json(new { success = true, active = category.IsActive });
        }

        [HttpPost("/api/settings/save")]
        public async Task<IActionResult> SaveSettings()
        {
            if (!IsAuthorizedAdminOrOwner())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            Dictionary<string, string>? settings = null;

            if (Request.HasFormContentType)
            {
                var form = await Request.ReadFormAsync();
                var settingsJson = form["settingsJson"].ToString();
                if (!string.IsNullOrWhiteSpace(settingsJson))
                {
                    try
                    {
                        settings = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(settingsJson);
                    }
                    catch { }
                }
            }
            else
            {
                try
                {
                    settings = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, string>>(Request.Body);
                }
                catch { }
            }

            if (settings == null || settings.Count == 0)
            {
                return BadRequest(new { success = false, message = "No settings provided." });
            }

            var targetInstituteId = UserInstituteId ?? 1;

            // If Organization Admin changed their organization's name, update the Institute entity and session
            if (!IsSuperAdmin() && settings.TryGetValue("OrganizationName", out var newOrgName) && !string.IsNullOrWhiteSpace(newOrgName))
            {
                var institute = await _context.Institutes.FindAsync(targetInstituteId);
                if (institute != null)
                {
                    institute.Name = newOrgName.Trim();
                    HttpContext.Session.SetString("OrganizationName", institute.Name);
                }
            }

            var existingSettings = await _context.SystemSettings.ToListAsync();

            foreach (var kvp in settings)
            {
                var match = existingSettings.FirstOrDefault(s => s.SettingKey == kvp.Key);
                if (match != null)
                {
                    match.SettingValue = kvp.Value ?? "";
                }
                else
                {
                    _context.SystemSettings.Add(new SystemSetting
                    {
                        SettingKey = kvp.Key,
                        SettingValue = kvp.Value ?? ""
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, message = "Settings saved successfully." });
        }
    }

    public class CategoryCreateModel
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Revenue";
    }

    public class CategoryEditModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class CategoryToggleModel
    {
        public int Id { get; set; }
    }
}

