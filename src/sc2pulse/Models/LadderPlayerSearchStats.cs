using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderPlayerSearchStats
    {
        [JsonPropertyName("rating")]
        public int? Rating { get; set; }

        [JsonPropertyName("gamesPlayed")]
        public int? GamesPlayed { get; set; }

        [JsonPropertyName("rank")]
        public League? Rank { get; set; }
    }
}