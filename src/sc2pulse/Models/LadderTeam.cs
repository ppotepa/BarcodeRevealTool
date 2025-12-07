using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderTeam
    {
        [JsonPropertyName("teamId")]
        public long TeamId { get; set; }

        [JsonPropertyName("teamLegacyUid")]
        public string? TeamLegacyUid { get; set; }

        [JsonPropertyName("wins")]
        public int Wins { get; set; }

        [JsonPropertyName("losses")]
        public int? Losses { get; set; }

        [JsonPropertyName("updated")]
        public DateTime? Updated { get; set; }

        [JsonPropertyName("members")]
        public List<LadderTeamMember> Members { get; set; } = new();
    }
}