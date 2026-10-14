

using Microsoft.AspNetCore.Mvc;

namespace CashManagementSystem.Controllers
{
    public class TransactionsController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction(nameof(Revenue));
        }

        public IActionResult Revenue()
        {
            ViewData["Title"] = "Revenue";
            ViewData["TransactionType"] = "Revenue";
            return View();
        }

        public IActionResult Expenses()
        {
            ViewData["Title"] = "Expenses";
            ViewData["TransactionType"] = "Expense";
            return View();
        }

        public IActionResult Create()
        {
            ViewData["Title"] = "Add Transaction";
            return View();
        }
    }
}
