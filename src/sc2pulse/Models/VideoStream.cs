using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class VideoStream
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("language")]
        public LocaleInfo? Language { get; set; }

        [JsonPropertyName("userId")]
        public string? UserId { get; set; }

        [JsonPropertyName("viewerCount")]
        public int? ViewerCount { get; set; }

        [JsonPropertyName("thumbnailUrl")]
        public string? ThumbnailUrl { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("userName")]
        public string? UserName { get; set; }

        [JsonPropertyName("profileImageUrl")]
        public string? ProfileImageUrl { get; set; }

        [JsonPropertyName("service")]
        public LinkType? Service { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
