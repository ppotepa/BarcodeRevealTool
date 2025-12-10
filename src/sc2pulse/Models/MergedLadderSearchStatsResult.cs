using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class MergedLadderSearchStatsResult
    {
        [JsonPropertyName("regionTeamCount")]
        public Dictionary<string, long>? RegionTeamCount { get; set; }

        [JsonPropertyName("leagueTeamCount")]
        public Dictionary<string, long>? LeagueTeamCount { get; set; }

        [JsonPropertyName("raceTeamCount")]
        public Dictionary<string, long>? RaceTeamCount { get; set; }

        [JsonPropertyName("regionGamesPlayed")]
        public Dictionary<string, long>? RegionGamesPlayed { get; set; }

        [JsonPropertyName("leagueGamesPlayed")]
        public Dictionary<string, long>? LeagueGamesPlayed { get; set; }

        [JsonPropertyName("raceGamesPlayed")]
        public Dictionary<string, long>? RaceGamesPlayed { get; set; }
    }
}
