namespace CashManagementSystem.Models
{
    public class Bill
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = "Unpaid"; // "Paid", "Partially Paid", "Unpaid", "Overdue"
        public string Category { get; set; } = "General";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int? InstituteId { get; set; }

        public List<BillPayment> Payments { get; set; } = new();

        public decimal OutstandingAmount => Math.Max(0, TotalAmount - PaidAmount);

        public string ComputeStatus()
        {
            if (PaidAmount >= TotalAmount) return "Paid";
            if (DueDate.Date < DateTime.Today) return "Overdue";
            if (PaidAmount > 0) return "Partially Paid";
            return "Unpaid";
        }
    }

    public class BillPayment
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; } = DateTime.Today;
        public string PaymentMethod { get; set; } = "Cash";
        public string? Notes { get; set; }
        public string? RecordedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Bill? Bill { get; set; }
    }
}
