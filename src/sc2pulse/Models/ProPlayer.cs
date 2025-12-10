using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class ProPlayer
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("aligulacId")]
        public long? AligulacId { get; set; }

        [JsonPropertyName("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("birthday")]
        public DateTime? Birthday { get; set; }

        [JsonPropertyName("earnings")]
        public int? Earnings { get; set; }

        [JsonPropertyName("updated")]
        public DateTime Updated { get; set; }

        [JsonPropertyName("version")]
        public int? Version { get; set; }
    }
}
