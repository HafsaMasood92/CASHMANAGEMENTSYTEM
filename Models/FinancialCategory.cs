namespace CashManagementSystem.Models
{
    public class FinancialCategory
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "Revenue"; // "Revenue", "Expense", "Bill"
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? InstituteId { get; set; }
    }
}

