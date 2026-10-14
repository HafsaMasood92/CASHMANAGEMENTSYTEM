
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class UnitsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Organizational Units";
            return View();
        }

        public IActionResult Dashboard(string? name)
        {
            string[] allowedUnits =
            {
                "School",
                "College",
                "BS Program",
                "Training Center",
                "Evening Academy"
            };

            if (string.IsNullOrWhiteSpace(name) ||
                !allowedUnits.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(Index));
            }

            var unitName = allowedUnits.First(unit =>
                string.Equals(unit, name, StringComparison.OrdinalIgnoreCase));

            ViewData["Title"] = unitName + " Dashboard";
            ViewData["UnitName"] = unitName;

            return View();
        }
    }
}
