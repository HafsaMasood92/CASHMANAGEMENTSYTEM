namespace CashManagementSystem.Models
{
    public class BankAccount
    {
        public int Id { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = "Cash"; // "Cash" or "Bank"
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? InstituteId { get; set; }
    }
}

