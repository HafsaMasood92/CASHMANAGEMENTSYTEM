
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class SettingsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Settings";
            return View();
        }
    }
}
