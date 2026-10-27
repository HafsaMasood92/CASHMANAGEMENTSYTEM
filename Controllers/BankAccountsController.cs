using CashManagementSystem.Data;
using CashManagementSystem.Filters;
using CashManagementSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CashManagementSystem.Controllers
{
    [AuthorizeSession]
    public class BankAccountsController : Controller
    {
        private readonly AppDbContext _context;

        public BankAccountsController(AppDbContext context)
        {
            _context = context;
        }

        private string UserRole => HttpContext.Session.GetString("UserRole") ?? "Super Admin";
        private int? InstituteId => HttpContext.Session.GetInt32("InstituteId");

        [HttpGet("/BankAccounts")]
        [HttpGet("/BankAccounts/Index")]
        public IActionResult Index()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                TempData["NoticeMessage"] = "Bank accounts and balances are restricted to Organization Admins and authorized accountants.";
                return RedirectToAction("Index", "Home");
            }

            ViewData["Title"] = "Bank Accounts";
            return View();
        }

        [HttpGet("/api/bankaccounts")]
        public async Task<IActionResult> GetAccounts()
        {
            if (UserRole == "Super Admin" || UserRole == "User")
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { success = false, message = "Access denied. You do not have access to organization bank accounts." });
            }

            var targetInstituteId = InstituteId ?? 1;
            var accounts = await _context.BankAccounts
                .Where(a => a.InstituteId == targetInstituteId)
                .OrderBy(a => a.Id)
                .ToListAsync();

            var allowedUnits = _context.InstituteUnits
                .Where(u => u.InstituteId == targetInstituteId)
                .Select(u => u.Name)
                .ToList();

            var transactions = await _context.Transactions
                .Where(t => t.InstituteId == targetInstituteId && allowedUnits.Contains(t.UnitName))
                .ToListAsync();

            var result = accounts.Select(account =>
            {
                var currentBalance = CalculateAccountBalance(account, transactions);

                return new
                {
                    id = account.Id,
                    name = account.AccountName,
                    type = account.AccountType,
                    bank = string.IsNullOrWhiteSpace(account.BankName) ? "—" : account.BankName,
                    number = string.IsNullOrWhiteSpace(account.AccountNumber) ? "—" : account.AccountNumber,
                    openingBalance = account.OpeningBalance,
                    currentBalance = currentBalance,
                    active = account.IsActive
                };
            }).ToList();

            var totalAccounts = result.Count;
            var totalBalance = result.Sum(a => a.currentBalance);
            var bankAccountsCount = result.Count(a => a.type == "Bank");

            return Json(new { items = result, totalAccounts, totalBalance, bankAccountsCount });
        }

        private static decimal CalculateAccountBalance(BankAccount account, List<Transaction> transactions)
        {
            var isCash = account.AccountType == "Cash";

            // Normal Inflows (Revenue)
            var inflow = transactions
                .Where(t => t.Type == "Revenue" && t.Category != "Account Transfer" &&
                    (t.PaymentMethod.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase) ||
                     (isCash && t.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase)) ||
                     (!isCash && account.AccountName.Contains("Meezan", StringComparison.OrdinalIgnoreCase) && t.PaymentMethod.Equals("Bank Account", StringComparison.OrdinalIgnoreCase))))
                .Sum(t => t.Amount);

            // Normal Outflows (Expenses)
            var outflow = transactions
                .Where(t => t.Type == "Expense" && t.Category != "Account Transfer" &&
                    (t.PaymentMethod.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase) ||
                     (isCash && t.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase)) ||
                     (!isCash && account.AccountName.Contains("Meezan", StringComparison.OrdinalIgnoreCase) && t.PaymentMethod.Equals("Bank Account", StringComparison.OrdinalIgnoreCase))))
                .Sum(t => t.Amount);

            // Transfers In
            var transferIn = transactions
                .Where(t => (t.Type == "Transfer" || t.Category == "Account Transfer") &&
                    t.ReferenceNumber != null && t.ReferenceNumber.EndsWith("-IN") &&
                    t.PaymentMethod.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            // Transfers Out
            var transferOut = transactions
                .Where(t => (t.Type == "Transfer" || t.Category == "Account Transfer") &&
                    t.ReferenceNumber != null && t.ReferenceNumber.EndsWith("-OUT") &&
                    t.PaymentMethod.Equals(account.AccountName, StringComparison.OrdinalIgnoreCase))
                .Sum(t => t.Amount);

            return account.OpeningBalance + inflow - outflow + transferIn - transferOut;
        }

        [HttpPost("/api/bankaccounts")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAccount([FromForm] string name, [FromForm] string type, [FromForm] string? bank, [FromForm] string? number, [FromForm] decimal openingBalance)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return BadRequest(new { success = false, message = "Account name is required." });
            }

            var targetInstituteId = InstituteId ?? 1;
            var account = new BankAccount
            {
                AccountName = name.Trim(),
                AccountType = type == "Bank" ? "Bank" : "Cash",
                BankName = (type == "Bank" ? bank : "Cash in Hand")?.Trim() ?? "—",
                AccountNumber = (type == "Bank" ? number : "—")?.Trim() ?? "—",
                OpeningBalance = Math.Max(0, openingBalance),
                IsActive = true,
                InstituteId = targetInstituteId,
                CreatedAt = DateTime.UtcNow
            };

            _context.BankAccounts.Add(account);
            await _context.SaveChangesAsync();

            return Json(new { success = true, accountId = account.Id });
        }

        [HttpPost("/api/bankaccounts/toggle-status")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAccountStatus([FromForm] int id)
        {
            var targetInstituteId = InstituteId ?? 1;
            var account = await _context.BankAccounts.FirstOrDefaultAsync(a => a.Id == id && a.InstituteId == targetInstituteId);
            if (account == null)
            {
                return NotFound(new { success = false, message = "Account not found." });
            }

            account.IsActive = !account.IsActive;
            await _context.SaveChangesAsync();

            return Json(new { success = true, active = account.IsActive });
        }

        [HttpPost("/api/bankaccounts/transfer")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferFunds([FromForm] int fromAccountId, [FromForm] int toAccountId, [FromForm] decimal amount, [FromForm] string? notes)
        {
            if (fromAccountId == toAccountId)
            {
                return BadRequest(new { success = false, message = "Source and destination accounts must be different." });
            }

            if (amount <= 0)
            {
                return BadRequest(new { success = false, message = "Transfer amount must be greater than zero." });
            }

            var fromAccount = await _context.BankAccounts.FindAsync(fromAccountId);
            var toAccount = await _context.BankAccounts.FindAsync(toAccountId);

            if (fromAccount == null || toAccount == null)
            {
                return NotFound(new { success = false, message = "One or both selected accounts do not exist." });
            }

            if (!fromAccount.IsActive || !toAccount.IsActive)
            {
                return BadRequest(new { success = false, message = "Both accounts must be active to complete a transfer." });
            }

            var targetInstituteId = InstituteId ?? 1;

            // Calculate current source account balance
            var transactions = await _context.Transactions
                .Where(t => t.InstituteId == targetInstituteId)
                .ToListAsync();
            var sourceBalance = CalculateAccountBalance(fromAccount, transactions);

            if (amount > sourceBalance)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Insufficient funds in source account ({fromAccount.AccountName}). Available balance is Rs. {sourceBalance:N0}, requested transfer is Rs. {amount:N0}."
                });
            }

            var userName = HttpContext.Session.GetString("UserName") ?? "Accountant";
            var refCode = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}";
            var description = string.IsNullOrWhiteSpace(notes) ? "Inter-account funds transfer" : notes.Trim();

            // 1. Debit from source account (Type = "Transfer" so it does not count as Expense)
            var debitTx = new Transaction
            {
                InstituteId = targetInstituteId,
                Description = $"Transfer to {toAccount.AccountName}: {description}",
                Amount = amount,
                Type = "Transfer",
                TransactionDate = DateTime.Today,
                UnitName = "All Units",
                PaymentMethod = fromAccount.AccountName,
                Category = "Account Transfer",
                ReferenceNumber = $"{refCode}-OUT",
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow
            };

            // 2. Credit to destination account (Type = "Transfer" so it does not count as Revenue)
            var creditTx = new Transaction
            {
                InstituteId = targetInstituteId,
                Description = $"Transfer from {fromAccount.AccountName}: {description}",
                Amount = amount,
                Type = "Transfer",
                TransactionDate = DateTime.Today,
                UnitName = "All Units",
                PaymentMethod = toAccount.AccountName,
                Category = "Account Transfer",
                ReferenceNumber = $"{refCode}-IN",
                CreatedBy = userName,
                CreatedAt = DateTime.UtcNow
            };

            _context.Transactions.AddRange(debitTx, creditTx);

            // 3. Notification log
            _context.Notifications.Add(new NotificationItem
            {
                InstituteId = targetInstituteId,
                Type = "Transfer",
                Title = $"Funds Transfer: Rs. {amount:N0}",
                Message = $"Transferred Rs. {amount:N0} from {fromAccount.AccountName} to {toAccount.AccountName}.",
                UnitName = "All Units",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return Json(new
            {
                success = true,
                message = $"Successfully transferred Rs. {amount:N0} from {fromAccount.AccountName} to {toAccount.AccountName}.",
                transferRef = refCode
            });
        }
    }
}
