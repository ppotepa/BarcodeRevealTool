namespace BarcodeRevealTool.Replay
{
    /// <summary>
    /// Fast metadata representation of a StarCraft 2 replay.
    /// </summary>
    public class ReplayMetadata
    {
        public string FilePath { get; set; } = string.Empty;
        public Guid ReplayGuid { get; set; } = Guid.Empty;
        public List<PlayerInfo> Players { get; set; } = new();
        public DateTime GameDate { get; set; }
        public string? SC2ClientVersion { get; set; }
        public DateTime LastModified { get; internal set; }

        /// <summary>
        /// Compute a deterministic GUID based on replay name and game date.
        /// This ensures the same replay always gets the same GUID.
        /// </summary>
        public static Guid ComputeDeterministicGuid(string replayName, DateTime gameDate)
        {
            // Create a deterministic GUID using MD5 of replay name + date
            using var md5 = System.Security.Cryptography.MD5.Create();
            var input = $"{Path.GetFileNameWithoutExtension(replayName)}:{gameDate:O}".ToLowerInvariant();
            var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));

            // Convert MD5 hash to GUID (first 16 bytes)
            return new Guid(hash);
        }
    }

    /// <summary>
    /// Information about a player in a replay.
    /// </summary>
    public class PlayerInfo
    {
        public string Name { get; set; } = string.Empty;
        public string BattleTag { get; set; } = string.Empty;
        public string Race { get; set; } = string.Empty;
    }
}