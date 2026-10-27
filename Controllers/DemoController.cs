using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class DemoController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}