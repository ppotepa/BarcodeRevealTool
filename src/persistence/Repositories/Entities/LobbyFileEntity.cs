namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents a lobby file binary stored in the database.
    /// </summary>
    public class LobbyFileEntity : BaseEntity
    {
        public int RunNumber { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public byte[] BinaryData { get; set; } = Array.Empty<byte>();
        public int MatchIndex { get; set; }
        public string? DetectedMapName { get; set; }
        public string? DetectedPlayer1 { get; set; }
        public string? DetectedPlayer2 { get; set; }
    }
}
