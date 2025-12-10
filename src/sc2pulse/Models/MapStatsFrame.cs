using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class MapStatsFrame
    {
        [JsonPropertyName("mapStatsFilmId")]
        public int MapStatsFilmId { get; set; }

        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("games")]
        public int Games { get; set; }
    }
}
