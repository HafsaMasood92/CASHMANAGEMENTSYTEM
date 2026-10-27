namespace CashManagementSystem.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
    }
}
