using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LeagueTier
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("minRating")]
        public int? MinRating { get; set; }

        [JsonPropertyName("maxRating")]
        public int? MaxRating { get; set; }

        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("leagueId")]
        public int LeagueId { get; set; }
    }
}
