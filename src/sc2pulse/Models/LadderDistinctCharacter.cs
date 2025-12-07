using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderDistinctCharacter
    {
        [JsonPropertyName("leagueMax")]
        public League? LeagueMax { get; set; }

        [JsonPropertyName("ratingMax")]
        public int? RatingMax { get; set; }

        [JsonPropertyName("totalGamesPlayed")]
        public int TotalGamesPlayed { get; set; }

        [JsonPropertyName("previousStats")]
        public LadderPlayerSearchStats? PreviousStats { get; set; }

        [JsonPropertyName("currentStats")]
        public LadderPlayerSearchStats? CurrentStats { get; set; }

        [JsonPropertyName("members")]
        public LadderTeamMember? Members { get; set; }

        public object BuildOrder { get; set; }
    }
}