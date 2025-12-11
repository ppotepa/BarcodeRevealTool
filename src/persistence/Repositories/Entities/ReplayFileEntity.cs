namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents a replay file record in the database.
    /// </summary>
    public class ReplayFileEntity : BaseEntity
    {
        public string? YourTag { get; set; }
        public string? OpponentTag { get; set; }
        public string? OpponentToon { get; set; }
        public string? OpponentNickname { get; set; }
        public string? Map { get; set; }
        public string? YourRace { get; set; }
        public string? OpponentRace { get; set; }
        public string? Result { get; set; }
        public DateTime GameDate { get; set; }
        public string? ReplayFilePath { get; set; }
        public string? Sc2ClientVersion { get; set; }
        public string? YouId { get; set; }
        public string? OpponentId { get; set; }
        public string? Winner { get; set; }
        public string? Note { get; set; }
    }
}
