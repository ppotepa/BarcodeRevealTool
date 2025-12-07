using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderMatch
    {
        [JsonPropertyName("match")]
        public Match? Match { get; set; }

        [JsonPropertyName("map")]
        public SC2Map? Map { get; set; }

        [JsonPropertyName("participants")]
        public List<LadderMatchParticipant> Participants { get; set; } = new();
    }

    public sealed class LadderMatchParticipant
    {
        [JsonPropertyName("participant")]
        public MatchParticipant? Participant { get; set; }

        [JsonPropertyName("team")]
        public LadderTeam? Team { get; set; }

        [JsonPropertyName("teamState")]
        public LadderTeamState? TeamState { get; set; }

        [JsonPropertyName("twitchVodUrl")]
        public string? TwitchVodUrl { get; set; }

        [JsonPropertyName("subOnlyTwitchVod")]
        public bool? SubOnlyTwitchVod { get; set; }
    }
}