using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class SC2Map
    {
        [JsonPropertyName("mapStatsFilmSpecId")]
        public long? MapStatsFilmSpecId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public sealed class Match
    {
        [JsonPropertyName("matchId")]
        public long? MatchId { get; set; }

        [JsonPropertyName("updated")]
        public DateTime? Updated { get; set; }

        [JsonPropertyName("participants")]
        public List<MatchParticipant>? Participants { get; set; }
    }

    public sealed class MatchParticipant
    {
        [JsonPropertyName("playerCharacterId")]
        public long PlayerCharacterId { get; set; }

        [JsonPropertyName("team")]
        public long? Team { get; set; }

        [JsonPropertyName("teamSlot")]
        public int? TeamSlot { get; set; }

        [JsonPropertyName("race")]
        public string? Race { get; set; }

        [JsonPropertyName("decision")]
        public bool? Decision { get; set; }
    }
}
