namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Metadata extracted from a StarCraft II replay file.
    /// Contains game information like players, map, race, timestamp, etc.
    /// </summary>
    public class ReplayMetadata
    {
        public string ReplayFilePath { get; set; }
        public string ReplayGuid { get; set; }
        public string YourPlayer { get; set; }
        public string OpponentPlayer { get; set; }
        public string Map { get; set; }
        public string YourRace { get; set; }
        public string OpponentRace { get; set; }
        public DateTime GameDate { get; set; }
        public string? SC2ClientVersion { get; set; }
        public string? YourPlayerId { get; set; }
        public string? OpponentPlayerId { get; set; }

        public ReplayMetadata()
        {
            ReplayFilePath = string.Empty;
            ReplayGuid = string.Empty;
            YourPlayer = string.Empty;
            OpponentPlayer = string.Empty;
            Map = string.Empty;
            YourRace = string.Empty;
            OpponentRace = string.Empty;
        }

        /// <summary>
        /// Compute a deterministic GUID for a replay based on filename and game date.
        /// This allows us to identify the same replay even if it's moved to different paths.
        /// </summary>
        public static string ComputeDeterministicGuid(string fileName, DateTime gameDate)
        {
            var combined = $"{fileName}_{gameDate:O}";
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(combined));
            return Convert.ToHexString(hash).Substring(0, 16);
        }
    }
}
