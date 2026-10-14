
namespace CashManagementSystem.Models
{
    public class Bill
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;

        public decimal OutstandingAmount => TotalAmount - PaidAmount;
    }
}
