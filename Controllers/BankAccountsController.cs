
using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class BankAccountsController : Controller
    {
        public IActionResult Index()
        {
            ViewData["Title"] = "Bank Accounts";
            return View();
        }
    }
}
