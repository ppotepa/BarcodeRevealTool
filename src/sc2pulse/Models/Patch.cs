using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class Patch
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("build")]
        public long Build { get; set; }

        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;

        [JsonPropertyName("versus")]
        public bool? Versus { get; set; }
    }
}
