
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class ReportsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Financial Reports";
            return View();
        }
    }
}
