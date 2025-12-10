using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class MapStatsFilm
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("mapId")]
        public int MapId { get; set; }

        [JsonPropertyName("leagueTierId")]
        public int LeagueTierId { get; set; }

        [JsonPropertyName("mapStatsFilmSpecId")]
        public int MapStatsFilmSpecId { get; set; }

        [JsonPropertyName("crossTier")]
        public bool CrossTier { get; set; }
    }
}
