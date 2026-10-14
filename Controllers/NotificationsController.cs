
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class NotificationsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Notifications";
            return View();
        }
    }
}
