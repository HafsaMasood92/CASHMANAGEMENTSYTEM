using CashManagementSystem.Data;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace CashManagementSystem.Controllers
{
    public class AccountController : Controller
    {
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // If already logged in, redirect to Dashboard
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("UserEmail")))
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string email, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                ViewBag.Error = "Please enter both email and password.";
                return View();
            }

            var cleanEmail = email.Trim().ToLower();
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email.ToLower() == cleanEmail && u.IsActive);

            if (user == null || user.PasswordHash != password)
            {
                ViewBag.Error = "Invalid email or password. Please check your credentials.";
                return View();
            }

            var cleanRole = user.Role;
            if (cleanRole == "Organization Owner") cleanRole = "Organization Admin";
            else if (cleanRole == "School Accountant" || cleanRole == "Multi-Unit Accountant") cleanRole = "Accountant";
            else if (cleanRole == "Single Unit User") cleanRole = "User";

            var finalRole = cleanRole;
            var finalEmail = user.Email;
            var finalName = user.FullName;

            // Unit permissions
            string assignedUnitsJson = user?.AssignedUnitsJson ?? "";
            List<string> userUnits = new();
            if (!string.IsNullOrWhiteSpace(assignedUnitsJson) && assignedUnitsJson != "[]")
            {
                try
            {
                    var parsed = JsonSerializer.Deserialize<List<string>>(assignedUnitsJson);
                    if (parsed != null && parsed.Any())
                    {
                        userUnits = parsed;
                    }
                }
                catch { }
            }

            if (!userUnits.Any())
            {
                if (finalRole == "Super Admin")
                {
                    userUnits = await _context.OrganizationalUnits
                        .Where(u => u.IsActive)
                        .Select(u => u.Name)
                        .ToListAsync();
                }
                else if (user?.InstituteId != null)
                {
                    userUnits = await _context.InstituteUnits
                        .Where(u => u.InstituteId == user.InstituteId.Value)
                        .Select(u => u.Name)
                        .ToListAsync();
                }

                if (!userUnits.Any())
                {
                    userUnits = new List<string>();
                }

                assignedUnitsJson = JsonSerializer.Serialize(userUnits);
            }

            // Sync userUnits to OrganizationalUnits table if missing
            foreach (var uName in userUnits)
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
            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("UserRole", finalRole);
            HttpContext.Session.SetString("UserEmail", finalEmail);
            HttpContext.Session.SetString("UserName", finalName);
            HttpContext.Session.SetString("AssignedUnits", assignedUnitsJson);

            if (user != null)
            {
                HttpContext.Session.SetInt32("UserId", user.Id);
                if (user.InstituteId.HasValue)
                {
                    HttpContext.Session.SetInt32("InstituteId", user.InstituteId.Value);
                    var inst = await _context.Institutes.FindAsync(user.InstituteId.Value);
                    if (inst != null)
                    {
                        HttpContext.Session.SetString("OrganizationName", inst.Name);
                    }
                }
            }

            if (finalRole == "Super Admin")
            {
                HttpContext.Session.SetString("OrganizationName", "Platform Administration");

                // Generate login notification for Super Admin (deduplicating recent unread login notifications)
                var hasRecentLogin = await _context.Notifications.AnyAsync(n =>
                    n.InstituteId == null &&
                    n.Type == "System" &&
                    n.Title == "Super Admin Login" &&
                    !n.IsRead &&
                    n.CreatedAt > DateTime.UtcNow.AddMinutes(-15));

                if (!hasRecentLogin)
                {
                    _context.Notifications.Add(new NotificationItem
                    {
                        Type = "System",
                        Title = "Super Admin Login",
                        Message = $"Super Admin logged in ({finalEmail}) at {DateTime.UtcNow:dd MMM yyyy, hh:mm tt} UTC.",
                        UnitName = "All Units",
                        IsRead = false,
                        IsActionRequired = false,
                        CreatedAt = DateTime.UtcNow,
                        InstituteId = null
                    });
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Index", "Home");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return Redirect("/Account/Login");
        }
    }
}
