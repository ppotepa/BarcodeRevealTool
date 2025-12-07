using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class TeamState
    {
        [JsonPropertyName("teamId")]
        public long TeamId { get; set; }

        [JsonPropertyName("dateTime")]
        public DateTime? DateTime { get; set; }

        [JsonPropertyName("divisionId")]
        public int? DivisionId { get; set; }

        [JsonPropertyName("wins")]
        public int? Wins { get; set; }

        [JsonPropertyName("games")]
        public int? Games { get; set; }

        [JsonPropertyName("rating")]
        public int? Rating { get; set; }

        [JsonPropertyName("globalRank")]
        public int? GlobalRank { get; set; }
    }

    public sealed class LadderTeamState
    {
        [JsonPropertyName("teamState")]
        public TeamState? TeamState { get; set; }

        [JsonPropertyName("race")]
        public string? Race { get; set; }

        [JsonPropertyName("league")]
        public string? League { get; set; }

        [JsonPropertyName("tier")]
        public object? Tier { get; set; }
    }
}
