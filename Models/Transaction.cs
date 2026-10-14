
namespace CashManagementSystem.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = string.Empty;
        public DateTime TransactionDate { get; set; } = DateTime.Today;
        public string PaymentMethod { get; set; } = string.Empty;
    }
}
