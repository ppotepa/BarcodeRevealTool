namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Represents a single build order entry extracted from a replay file.
    /// </summary>
    public class BuildOrderEntry
    {
        public string PlayerId { get; set; }
        public int TimeSeconds { get; set; }
        public string Kind { get; set; }
        public string Name { get; set; }

        public BuildOrderEntry()
        {
            PlayerId = string.Empty;
            Kind = string.Empty;
            Name = string.Empty;
        }

        public BuildOrderEntry(string playerId, int timeSeconds, string kind, string name)
        {
            PlayerId = playerId;
            TimeSeconds = timeSeconds;
            Kind = kind;
            Name = name;
        }
    }
}
