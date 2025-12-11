using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class Clan
    {
        [JsonPropertyName("tag")]
        public string Tag { get; set; } = string.Empty;

        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("region")]
        public Region Region { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("members")]
        public int? Members { get; set; }

        [JsonPropertyName("activeMembers")]
        public int? ActiveMembers { get; set; }

        [JsonPropertyName("avgRating")]
        public int? AvgRating { get; set; }

        [JsonPropertyName("avgLeagueType")]
        public int? AvgLeagueType { get; set; }

        [JsonPropertyName("games")]
        public int? Games { get; set; }
    }
}
