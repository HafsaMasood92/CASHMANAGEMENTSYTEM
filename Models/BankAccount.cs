
namespace CashManagementSystem.Models
{
    public class BankAccount
    {
        public int Id { get; set; }

        public string AccountName { get; set; } = string.Empty;

        public string BankName { get; set; } = string.Empty;

        public string AccountNumber { get; set; } = string.Empty;

        public decimal OpeningBalance { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
