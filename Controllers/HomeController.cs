
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Dashboard";
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }
    }
}
