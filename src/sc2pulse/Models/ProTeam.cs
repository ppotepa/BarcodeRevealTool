using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class ProTeam
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("shortName")]
        public string? ShortName { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("aligulacId")]
        public long? AligulacId { get; set; }

        [JsonPropertyName("updated")]
        public DateTime Updated { get; set; }
    }
}
