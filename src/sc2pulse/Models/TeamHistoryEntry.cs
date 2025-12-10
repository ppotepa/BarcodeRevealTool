using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class TeamHistoryEntry
    {
        [JsonPropertyName("staticData")]
        public JsonElement StaticData { get; set; }

        [JsonPropertyName("history")]
        public JsonElement History { get; set; }
    }
}
