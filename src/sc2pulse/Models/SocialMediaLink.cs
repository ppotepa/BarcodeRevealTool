using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class SocialMediaLink
    {
        [JsonPropertyName("proPlayerId")]
        public long ProPlayerId { get; set; }

        [JsonPropertyName("type")]
        public LinkType Type { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("updated")]
        public DateTime Updated { get; set; }

        [JsonPropertyName("serviceUserId")]
        public string? ServiceUserId { get; set; }

        [JsonPropertyName("protected")]
        public bool? Protected { get; set; }
    }
}
