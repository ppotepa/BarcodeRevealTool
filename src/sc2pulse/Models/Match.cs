using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class SC2Map
    {
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public sealed class Match
    {
        [JsonPropertyName("date")]
        public DateTime? Date { get; set; }

        [JsonPropertyName("type")]
        public MatchKind Type { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("mapId")]
        public int? MapId { get; set; }

        [JsonPropertyName("region")]
        public Region Region { get; set; }

        [JsonPropertyName("updated")]
        public DateTime? Updated { get; set; }

        [JsonPropertyName("duration")]
        public int? Duration { get; set; }
    }

    public sealed class MatchParticipant
    {
        [JsonPropertyName("matchId")]
        public long? MatchId { get; set; }

        [JsonPropertyName("playerCharacterId")]
        public long? PlayerCharacterId { get; set; }

        [JsonPropertyName("teamId")]
        public long? TeamId { get; set; }

        [JsonPropertyName("teamStateDateTime")]
        public DateTime? TeamStateDateTime { get; set; }

        [JsonPropertyName("decision")]
        public string? Decision { get; set; }

        [JsonPropertyName("ratingChange")]
        public int? RatingChange { get; set; }
    }
}
