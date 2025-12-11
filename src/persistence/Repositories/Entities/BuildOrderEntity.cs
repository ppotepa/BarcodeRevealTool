namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents a build order entry from a replay.
    /// </summary>
    public class BuildOrderEntity : BaseEntity
    {
        public string? OpponentTag { get; set; }
        public string? OpponentNickname { get; set; }
        public int TimeSeconds { get; set; }
        public string? Kind { get; set; }
        public string? Name { get; set; }
        public string? ReplayFilePath { get; set; }
        public DateTime RecordedAt { get; set; }
    }
}
