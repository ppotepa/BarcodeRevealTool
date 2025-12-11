namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents a configuration history record tracking when user config changed.
    /// </summary>
    public class ConfigHistoryEntity : BaseEntity
    {
        public int RunNumber { get; set; }
        public string ConfigKey { get; set; } = string.Empty;
        public string? OldValue { get; set; }
        public string? NewValue { get; set; }
        public string ChangeSource { get; set; } = string.Empty; // Startup, Manual, Detected, etc
        public string? ChangeDetails { get; set; }
    }
}
