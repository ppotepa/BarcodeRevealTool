namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents a debug session - one run of the application tracking lobby files and matches.
    /// </summary>
    public class DebugSessionEntity : BaseEntity
    {
        public int RunNumber { get; set; }
        public string? PresetUserBattleTag { get; set; }
        public int TotalMatchesPlayed { get; set; }
        public int TotalLobbiesProcessed { get; set; }
        public string Status { get; set; } = "InProgress"; // InProgress, Completed, Failed
        public int? ExitCode { get; set; }
    }
}
