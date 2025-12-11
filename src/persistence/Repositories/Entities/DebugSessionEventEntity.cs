namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents an event logged during a debug session (lobby detected, match finished, etc).
    /// </summary>
    public class DebugSessionEventEntity : BaseEntity
    {
        public long DebugSessionId { get; set; }
        public string EventType { get; set; } = string.Empty; // LobbyDetected, MatchFinished, etc
        public string? EventDetails { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
