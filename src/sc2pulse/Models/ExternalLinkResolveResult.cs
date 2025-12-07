using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class PlayerCharacterLink
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }
    }

    public sealed class ExternalLinkResolveResult
    {
        [JsonPropertyName("playerCharacterId")]
        public long? PlayerCharacterId { get; set; }

        [JsonPropertyName("links")]
        public List<PlayerCharacterLink> Links { get; set; } = new();

        [JsonPropertyName("failedTypes")]
        public List<string> FailedTypes { get; set; } = new();
    }
}