using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class TeamHistorySummaryEntry
    {
        [JsonPropertyName("staticData")]
        public JsonElement StaticData { get; set; }

        [JsonPropertyName("summary")]
        public JsonElement Summary { get; set; }
    }
}
