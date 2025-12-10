using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class QueueStats
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("season")]
        public int Season { get; set; }

        [JsonPropertyName("queueType")]
        public string QueueType { get; set; } = string.Empty;

        [JsonPropertyName("teamType")]
        public string TeamType { get; set; } = string.Empty;

        [JsonPropertyName("playerBase")]
        public long PlayerBase { get; set; }

        [JsonPropertyName("playerCount")]
        public int PlayerCount { get; set; }

        [JsonPropertyName("lowActivityPlayerCount")]
        public int LowActivityPlayerCount { get; set; }

        [JsonPropertyName("mediumActivityPlayerCount")]
        public int MediumActivityPlayerCount { get; set; }

        [JsonPropertyName("highActivityPlayerCount")]
        public int HighActivityPlayerCount { get; set; }
    }
}
