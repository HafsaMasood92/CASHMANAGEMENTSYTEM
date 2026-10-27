namespace CashManagementSystem.Models
{
    public class NotificationItem
    {
        public int Id { get; set; }
        public string Type { get; set; } = "System"; // "Transaction", "Deletion", "Bill", "System"
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string UnitName { get; set; } = "All Units";
        public bool IsRead { get; set; } = false;
        public bool IsActionRequired { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? InstituteId { get; set; }
    }
}
