
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class BillsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Bills & Payments";
            return View();
        }
    }
}
