namespace CashManagementSystem.Models
{
    public class User
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty; // "Super Admin", "Organization Owner", "School Accountant", "Multi-Unit Accountant"
        public int? InstituteId { get; set; }
        public string AssignedUnitsJson { get; set; } = "[]"; // List of allowed unit names or IDs in JSON
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Institute? Institute { get; set; }
    }
}
