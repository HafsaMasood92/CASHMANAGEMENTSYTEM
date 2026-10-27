namespace CashManagementSystem.Models
{
    public class Transaction
    {
        public int Id { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Type { get; set; } = "Revenue"; // "Revenue" or "Expense"
        public DateTime TransactionDate { get; set; } = DateTime.Today;
        public string UnitName { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Cash"; // "Cash", "Bank Account", etc.
        public string Category { get; set; } = "General";
        public string? ReferenceNumber { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? InstituteId { get; set; }
    }
}
