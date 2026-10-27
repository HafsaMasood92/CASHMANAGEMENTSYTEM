namespace CashManagementSystem.Models
{
    public class Institute
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<InstituteUnit> Units { get; set; } = new();
        public List<User> Admins { get; set; } = new();
    }

    public class InstituteUnit
    {
        public int Id { get; set; }
        public int InstituteId { get; set; }
        public string Name { get; set; } = string.Empty;

        public Institute? Institute { get; set; }
    }
}
