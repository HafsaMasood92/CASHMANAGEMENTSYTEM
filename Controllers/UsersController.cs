using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession(Roles = "Super Admin,Organization Owner,Organization Admin")]
    public class UsersController : Controller
    {
        private readonly AppDbContext _context;

        public UsersController(AppDbContext context)
        {
            _context = context;
        }

        private string? UserEmail => HttpContext.Session.GetString("UserEmail");
        private string UserRole => HttpContext.Session.GetString("UserRole") ?? "";
        private int? UserInstituteId => HttpContext.Session.GetInt32("InstituteId");

        private bool IsAuthorizedSuperAdmin() => !string.IsNullOrEmpty(UserEmail) && UserRole == "Super Admin";
        private bool IsAuthorizedOrgAdmin() => !string.IsNullOrEmpty(UserEmail) && (UserRole == "Organization Owner" || UserRole == "Organization Admin");

        public IActionResult Index()
        {
            if (string.IsNullOrEmpty(UserEmail))
            {
                return RedirectToAction("Login", "Account");
            }

            if (!IsAuthorizedSuperAdmin() && !IsAuthorizedOrgAdmin())
            {
                TempData["NoticeMessage"] = "Access restricted. User administration is reserved for Super Admins and Organization Admins.";
                return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = IsAuthorizedSuperAdmin() ? "Manage Organizations & Admins" : "Manage Users";
            ViewBag.IsSuperAdmin = IsAuthorizedSuperAdmin();
            ViewBag.UserRole = IsAuthorizedSuperAdmin() ? "Super Admin" : "Organization Admin";
            return View();
        }

        [HttpGet("/api/users/data")]
        public async Task<IActionResult> GetUsersData()
        {
            if (!IsAuthorizedSuperAdmin() && !IsAuthorizedOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var isSuperAdmin = IsAuthorizedSuperAdmin();
            var targetInstituteId = UserInstituteId ?? 1;

            if (isSuperAdmin)
            {
                // Super Admin: Manages Organizations and Organization Admins only
                var institutesRaw = await _context.Institutes
                    .Include(i => i.Units)
                    .OrderBy(i => i.Id)
                    .ToListAsync();

                var institutes = institutesRaw.Select(i =>
                {
                    var admin = _context.Users.FirstOrDefault(u => u.InstituteId == i.Id && (u.Role == "Organization Admin" || u.Role == "Organization Owner"));
                    return new
                    {
                        id = i.Id,
                        name = i.Name,
                        units = i.Units.Select(u => new { id = u.Id, name = u.Name }).ToList(),
                        adminCount = _context.Users.Count(u => u.InstituteId == i.Id && (u.Role == "Organization Admin" || u.Role == "Organization Owner")),
                        ownerName = admin?.FullName ?? "Unassigned",
                        ownerEmail = admin?.Email ?? "N/A"
                    };
                }).ToList();

                var admins = await _context.Users
                    .Where(u => u.Role == "Organization Admin" || u.Role == "Organization Owner")
                    .OrderBy(u => u.Id)
                    .Select(u => new
                    {
                        id = u.Id,
                        instituteId = u.InstituteId ?? 1,
                        instituteName = u.Institute != null ? u.Institute.Name : "General",
                        name = u.FullName,
                        email = u.Email,
                        role = "Organization Admin",
                        assignedUnits = u.AssignedUnitsJson,
                        active = u.IsActive
                    })
                    .ToListAsync();

                var allUnits = await _context.OrganizationalUnits
                    .Where(u => u.IsActive)
                    .Select(u => new { id = u.Id, name = u.Name })
                    .ToListAsync();

                return Json(new { success = true, isSuperAdmin = true, institutes, admins, users = admins, allUnits });
            }
            else
            {
                // Organization Admin: Manages their organization's staff (Accountant and User) only
                var institutesRaw = await _context.Institutes
                    .Include(i => i.Units)
                    .Where(i => i.Id == targetInstituteId)
                    .ToListAsync();

                var institutes = institutesRaw.Select(i => new
                {
                    id = i.Id,
                    name = i.Name,
                    units = i.Units.Select(u => new { id = u.Id, name = u.Name }).ToList(),
                    adminCount = _context.Users.Count(u => u.InstituteId == i.Id && u.Role != "Super Admin" && u.Role != "Organization Admin" && u.Role != "Organization Owner"),
                    ownerName = HttpContext.Session.GetString("UserName") ?? "Organization Admin",
                    ownerEmail = UserEmail ?? "N/A"
                }).ToList();

                var staffUsers = await _context.Users
                    .Where(u => u.InstituteId == targetInstituteId && u.Role != "Super Admin" && u.Role != "Organization Admin" && u.Role != "Organization Owner")
                    .OrderBy(u => u.Id)
                    .Select(u => new
                    {
                        id = u.Id,
                        instituteId = u.InstituteId ?? targetInstituteId,
                        instituteName = u.Institute != null ? u.Institute.Name : "Organization",
                        name = u.FullName,
                        email = u.Email,
                        role = (u.Role == "User" || u.Role == "Single Unit User") ? "User" : "Accountant",
                        assignedUnits = u.AssignedUnitsJson,
                        active = u.IsActive
                    })
                    .ToListAsync();

                var orgUnits = await _context.InstituteUnits
                    .Where(u => u.InstituteId == targetInstituteId)
                    .Select(u => new { id = u.Id, name = u.Name })
                    .ToListAsync();

                if (!orgUnits.Any())
                {
                    orgUnits = await _context.OrganizationalUnits
                        .Where(u => u.IsActive)
                        .Select(u => new { id = u.Id, name = u.Name })
                        .ToListAsync();
                }

                return Json(new { success = true, isSuperAdmin = false, institutes, admins = staffUsers, users = staffUsers, allUnits = orgUnits });
            }
        }

        [HttpPost("/api/users/institutes")]
        public async Task<IActionResult> CreateInstitute([FromBody] InstituteCreateModel model)
        {
            if (!IsAuthorizedSuperAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { success = false, message = "Institute name is required." });
            }

            var institute = new Institute
            {
                Name = model.Name.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            if (model.Units != null && model.Units.Any())
            {
                foreach (var unitName in model.Units.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    var trimmed = unitName.Trim();
                    institute.Units.Add(new InstituteUnit { Name = trimmed });

                    if (!_context.OrganizationalUnits.Any(ou => ou.Name.ToLower() == trimmed.ToLower()))
                    {
                        _context.OrganizationalUnits.Add(new OrganizationalUnit
                        {
                            Name = trimmed,
                            Category = "Organization Unit",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            _context.Institutes.Add(institute);
            await _context.SaveChangesAsync();

            return Json(new { success = true, instituteId = institute.Id });
        }

        [HttpPost("/api/users/institutes/edit")]
        public async Task<IActionResult> EditInstitute([FromBody] InstituteEditModel model)
        {
            if (!IsAuthorizedSuperAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var institute = await _context.Institutes
                .Include(i => i.Units)
                .FirstOrDefaultAsync(i => i.Id == model.Id);

            if (institute == null)
            {
                return NotFound(new { success = false, message = "Institute not found." });
            }

            if (string.IsNullOrWhiteSpace(model.Name))
            {
                return BadRequest(new { success = false, message = "Institute name is required." });
            }

            institute.Name = model.Name.Trim();

            // Update units if provided
            if (model.Units != null)
            {
                institute.Units.Clear();
                foreach (var uName in model.Units.Where(u => !string.IsNullOrWhiteSpace(u)))
                {
                    var trimmed = uName.Trim();
                    institute.Units.Add(new InstituteUnit { Name = trimmed });

                    if (!_context.OrganizationalUnits.Any(ou => ou.Name.ToLower() == trimmed.ToLower()))
                    {
                        _context.OrganizationalUnits.Add(new OrganizationalUnit
                        {
                            Name = trimmed,
                            Category = "Organization Unit",
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/api/users/institutes/delete")]
        public async Task<IActionResult> DeleteInstitute([FromBody] IdModel model)
        {
            if (!IsAuthorizedSuperAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied. Only Super Admin can delete an organization." });
            }

            var institute = await _context.Institutes
                .Include(i => i.Units)
                .FirstOrDefaultAsync(i => i.Id == model.Id);

            if (institute == null)
            {
                return NotFound(new { success = false, message = "Organization not found." });
            }

            var count = await _context.Institutes.CountAsync();
            if (count <= 1)
            {
                return BadRequest(new { success = false, message = "Cannot delete the only remaining organization in the system." });
            }

            // Check if genuine financial records exist strictly for this organization
            var hasTransactions = await _context.Transactions
                .AnyAsync(t => t.InstituteId == model.Id);
            if (hasTransactions)
            {
                return BadRequest(new { success = false, message = $"Cannot delete organization '{institute.Name}'. Active financial transactions are recorded under it. Financial records cannot be removed." });
            }

            var hasBills = await _context.Bills
                .AnyAsync(b => b.InstituteId == model.Id);
            if (hasBills)
            {
                return BadRequest(new { success = false, message = $"Cannot delete organization '{institute.Name}'. Active bills or invoice payments are recorded under it. Financial records cannot be removed." });
            }

            var hasBankBalances = await _context.BankAccounts
                .AnyAsync(b => b.InstituteId == model.Id && b.OpeningBalance > 0);
            if (hasBankBalances)
            {
                return BadRequest(new { success = false, message = $"Cannot delete organization '{institute.Name}'. Active bank accounts with non-zero balances are recorded under it. Financial records cannot be removed." });
            }

            // Safe to delete: remove associated users, units, empty bank accounts, categories, and the institute
            var orgUsers = await _context.Users
                .Where(u => u.InstituteId == model.Id)
                .ToListAsync();
            if (orgUsers.Any())
            {
                _context.Users.RemoveRange(orgUsers);
            }

            var orgUnits = await _context.InstituteUnits
                .Where(u => u.InstituteId == model.Id)
                .ToListAsync();
            if (orgUnits.Any())
            {
                _context.InstituteUnits.RemoveRange(orgUnits);
            }

            var orgBankAccounts = await _context.BankAccounts
                .Where(b => b.InstituteId == model.Id)
                .ToListAsync();
            if (orgBankAccounts.Any())
            {
                _context.BankAccounts.RemoveRange(orgBankAccounts);
            }

            var orgCategories = await _context.FinancialCategories
                .Where(c => c.InstituteId == model.Id)
                .ToListAsync();
            if (orgCategories.Any())
            {
                _context.FinancialCategories.RemoveRange(orgCategories);
            }

            _context.Institutes.Remove(institute);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Organization '{institute.Name}' was deleted successfully." });
        }

        [HttpPost("/api/users/admins")]
        public async Task<IActionResult> CreateAdmin([FromBody] AdminCreateModel model)
        {
            if (!IsAuthorizedSuperAdmin() && !IsAuthorizedOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Email))
            {
                return BadRequest(new { success = false, message = "Name and email are required." });
            }

            var targetInstituteId = IsAuthorizedSuperAdmin() ? model.InstituteId : (UserInstituteId ?? 1);

            var exists = await _context.Users.AnyAsync(u => u.Email.ToLower() == model.Email.Trim().ToLower());
            if (exists)
            {
                return BadRequest(new { success = false, message = "A user with this email address already exists." });
            }

            var assignedUnits = model.Units != null && model.Units.Any(u => !string.IsNullOrWhiteSpace(u))
                ? model.Units.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList()
                : (await _context.InstituteUnits.Where(iu => iu.InstituteId == targetInstituteId).Select(iu => iu.Name).ToListAsync());

            var assignedUnitsJson = JsonSerializer.Serialize(assignedUnits);

            foreach (var uName in assignedUnits)
            {
                if (!await _context.OrganizationalUnits.AnyAsync(ou => ou.Name.ToLower() == uName.ToLower()))
                {
                    _context.OrganizationalUnits.Add(new OrganizationalUnit
                    {
                        Name = uName,
                        Category = "Organization Unit",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            string role;
            if (IsAuthorizedSuperAdmin())
            {
                // Super Admin creates Organization Admins only
                role = "Organization Admin";
            }
            else
            {
                // Organization Admin creates Accountant or User only
                var reqRole = (model.Role ?? "").Trim();
                role = reqRole.Equals("User", StringComparison.OrdinalIgnoreCase) ? "User" : "Accountant";
            }

            var password = string.IsNullOrWhiteSpace(model.Password) ? "Demo@123" : model.Password.Trim();

            var user = new User
            {
                FullName = model.Name.Trim(),
                Email = model.Email.Trim().ToLower(),
                PasswordHash = password,
                Role = role,
                InstituteId = targetInstituteId,
                AssignedUnitsJson = assignedUnitsJson,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);

            // Super Admin receives notification when an Organization Admin/Owner account is created
            if (role == "Organization Admin" || role == "Organization Owner")
            {
                var inst = await _context.Institutes.FindAsync(targetInstituteId);
                var orgName = inst?.Name ?? $"Organization #{targetInstituteId}";
                var notifTitle = $"Organization Admin Account Created: {user.FullName}";
                var notifMsg = $"Organization Admin account for '{user.FullName}' ({user.Email}) was created for organization '{orgName}'.";

                var existsNotif = await _context.Notifications.AnyAsync(n =>
                    n.InstituteId == null &&
                    n.Title == notifTitle &&
                    n.Message == notifMsg &&
                    n.CreatedAt > DateTime.UtcNow.AddMinutes(-10));

                if (!existsNotif)
                {
                    _context.Notifications.Add(new NotificationItem
                    {
                        Type = "System",
                        Title = notifTitle,
                        Message = notifMsg,
                        UnitName = "All Units",
                        IsRead = false,
                        IsActionRequired = false,
                        CreatedAt = DateTime.UtcNow,
                        InstituteId = null
                    });
                }
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, adminId = user.Id });
        }

        [HttpPost("/api/users/admins/edit")]
        public async Task<IActionResult> EditAdmin([FromBody] AdminEditModel model)
        {
            if (!IsAuthorizedSuperAdmin() && !IsAuthorizedOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var user = await _context.Users.FindAsync(model.Id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            if (IsAuthorizedSuperAdmin())
            {
                // Super Admin can only edit Organization Admins
                if (user.Role != "Organization Admin" && user.Role != "Organization Owner")
                {
                    return StatusCode(403, new { success = false, message = "Super Admins can only edit Organization Admin accounts directly. Organization accountants and users are managed by their respective Organization Admin." });
                }
            }
            else if (IsAuthorizedOrgAdmin())
            {
                // Org Admin can only edit their own organization's staff (Accountant and User)
                if (user.InstituteId != (UserInstituteId ?? 1) || user.Role == "Super Admin" || user.Role == "Organization Admin" || user.Role == "Organization Owner")
                {
                    return StatusCode(403, new { success = false, message = "You can only modify Accountant and User accounts within your organization." });
                }
            }

            if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Email))
            {
                return BadRequest(new { success = false, message = "Name and email are required." });
            }

            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == model.Email.Trim().ToLower() && u.Id != model.Id);
            if (emailExists)
            {
                return BadRequest(new { success = false, message = "This email is already in use by another user." });
            }

            user.FullName = model.Name.Trim();
            user.Email = model.Email.Trim().ToLower();

            if (IsAuthorizedSuperAdmin())
            {
                user.InstituteId = model.InstituteId;
                user.Role = "Organization Admin";
            }
            else if (IsAuthorizedOrgAdmin())
            {
                var reqRole = (model.Role ?? "").Trim();
                user.Role = reqRole.Equals("User", StringComparison.OrdinalIgnoreCase) ? "User" : "Accountant";
            }

            var editTargetInstituteId = user.InstituteId ?? (IsAuthorizedSuperAdmin() ? model.InstituteId : (UserInstituteId ?? 1));
            var editUnits = model.Units != null && model.Units.Any(u => !string.IsNullOrWhiteSpace(u))
                ? model.Units.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).ToList()
                : (await _context.InstituteUnits.Where(iu => iu.InstituteId == editTargetInstituteId).Select(iu => iu.Name).ToListAsync());

            user.AssignedUnitsJson = JsonSerializer.Serialize(editUnits);

            foreach (var uName in editUnits)
            {
                if (!await _context.OrganizationalUnits.AnyAsync(ou => ou.Name.ToLower() == uName.ToLower()))
                {
                    _context.OrganizationalUnits.Add(new OrganizationalUnit
                    {
                        Name = uName,
                        Category = "Organization Unit",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            if (!string.IsNullOrWhiteSpace(model.Password))
            {
                user.PasswordHash = model.Password.Trim();
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost("/api/users/admins/toggle-status")]
        public async Task<IActionResult> ToggleAdminStatus([FromBody] IdModel model)
        {
            if (!IsAuthorizedSuperAdmin() && !IsAuthorizedOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var user = await _context.Users.FindAsync(model.Id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            if (user.Role == "Super Admin")
            {
                return BadRequest(new { success = false, message = "Cannot disable the Super Admin account." });
            }

            if (IsAuthorizedSuperAdmin())
            {
                if (user.Role != "Organization Admin" && user.Role != "Organization Owner")
                {
                    return StatusCode(403, new { success = false, message = "Super Admin manages Organization Admins only." });
                }
            }
            else if (IsAuthorizedOrgAdmin())
            {
                if (user.InstituteId != (UserInstituteId ?? 1) || user.Role == "Super Admin" || user.Role == "Organization Admin" || user.Role == "Organization Owner")
                {
                    return StatusCode(403, new { success = false, message = "Access denied. You can only manage accounts within your organization." });
                }
            }

            user.IsActive = !user.IsActive;
            await _context.SaveChangesAsync();

            return Json(new { success = true, active = user.IsActive });
        }

        [HttpPost("/api/users/admins/delete")]
        public async Task<IActionResult> DeleteAdmin([FromBody] IdModel model)
        {
            if (!IsAuthorizedSuperAdmin() && !IsAuthorizedOrgAdmin())
            {
                return StatusCode(403, new { success = false, message = "Access denied." });
            }

            var user = await _context.Users.FindAsync(model.Id);
            if (user == null)
            {
                return NotFound(new { success = false, message = "User not found." });
            }

            if (user.Role == "Super Admin")
            {
                return BadRequest(new { success = false, message = "Cannot delete the Super Admin account." });
            }

            if (IsAuthorizedSuperAdmin())
            {
                if (user.Role != "Organization Admin" && user.Role != "Organization Owner")
                {
                    return StatusCode(403, new { success = false, message = "Super Admin can only delete Organization Admins." });
                }
            }
            else if (IsAuthorizedOrgAdmin())
            {
                if (user.InstituteId != (UserInstituteId ?? 1) || user.Role == "Super Admin" || user.Role == "Organization Admin" || user.Role == "Organization Owner")
                {
                    return StatusCode(403, new { success = false, message = "Access denied. You can only delete accounts within your organization." });
                }
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }

    public class InstituteCreateModel
    {
        public string Name { get; set; } = string.Empty;
        public List<string>? Units { get; set; }
    }

    public class InstituteEditModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public List<string>? Units { get; set; }
    }

    public class AdminCreateModel
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? Role { get; set; }
        public int InstituteId { get; set; }
        public List<string>? Units { get; set; }
    }

    public class AdminEditModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Password { get; set; }
        public string? Role { get; set; }
        public int InstituteId { get; set; }
        public List<string>? Units { get; set; }
    }

    public class IdModel
    {
        public int Id { get; set; }
    }
}
