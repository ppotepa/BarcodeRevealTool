using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderMapStatsFilm
    {
        [JsonPropertyName("maps")]
        public Dictionary<string, SC2Map>? Maps { get; set; }

        [JsonPropertyName("seasons")]
        public Dictionary<string, Season>? Seasons { get; set; }

        [JsonPropertyName("leagues")]
        public Dictionary<string, League>? Leagues { get; set; }

        [JsonPropertyName("tiers")]
        public Dictionary<string, LeagueTier>? Tiers { get; set; }

        [JsonPropertyName("specs")]
        public Dictionary<string, MapStatsFilmSpec>? Specs { get; set; }

        [JsonPropertyName("films")]
        public Dictionary<string, MapStatsFilm>? Films { get; set; }

        [JsonPropertyName("frames")]
        public List<MapStatsFrame> Frames { get; set; } = new();
    }
}
