using System;

namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Database representation of a stored replay record.
    /// </summary>
    public class ReplayRecord
    {
        public long Id { get; set; }
        public string ReplayGuid { get; set; }
        public string YourPlayer { get; set; }
        public string OpponentPlayer { get; set; }
        public string Map { get; set; }
        public string YourRace { get; set; }
        public string OpponentRace { get; set; }
        public DateTime GameDate { get; set; }
        public string ReplayFilePath { get; set; }
        public string? FileHash { get; set; }
        public string? SC2ClientVersion { get; set; }
        public string? YourPlayerId { get; set; }
        public string? OpponentPlayerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public ReplayRecord()
        {
            ReplayGuid = string.Empty;
            YourPlayer = string.Empty;
            OpponentPlayer = string.Empty;
            Map = string.Empty;
            YourRace = string.Empty;
            OpponentRace = string.Empty;
            ReplayFilePath = string.Empty;
        }
    }
}
