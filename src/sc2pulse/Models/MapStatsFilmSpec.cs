using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class MapStatsFilmSpec
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("race")]
        public Race Race { get; set; }

        [JsonPropertyName("versusRace")]
        public Race VersusRace { get; set; }

        [JsonPropertyName("frameDuration")]
        public JsonElement FrameDuration { get; set; }
    }
}
