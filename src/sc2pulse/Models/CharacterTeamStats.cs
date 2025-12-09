using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    /// <summary>
    /// Character team data from character-teams endpoint.
    /// Each team entry represents a player's 1v1 ladder entry for a specific race/season.
    /// </summary>
    public sealed class CharacterTeamStats
    {
        [JsonPropertyName("rating")]
        public int? Rating { get; set; }

        [JsonPropertyName("wins")]
        public int? Wins { get; set; }

        [JsonPropertyName("losses")]
        public int? Losses { get; set; }

        [JsonPropertyName("ties")]
        public int? Ties { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("divisionId")]
        public long? DivisionId { get; set; }

        [JsonPropertyName("season")]
        public int? Season { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("league")]
        public LeagueInfo? League { get; set; }

        [JsonPropertyName("globalRank")]
        public int? GlobalRank { get; set; }

        [JsonPropertyName("regionRank")]
        public int? RegionRank { get; set; }

        [JsonPropertyName("leagueRank")]
        public int? LeagueRank { get; set; }

        [JsonPropertyName("lastPlayed")]
        public DateTime? LastPlayed { get; set; }

        [JsonPropertyName("queueType")]
        public int? QueueType { get; set; }  // 201=1v1, 202=2v2, etc.

        [JsonPropertyName("teamType")]
        public int? TeamType { get; set; }  // 0=solo, 1=team

        [JsonPropertyName("leagueType")]
        public int? LeagueType { get; set; }  // 0=bronze, 1=silver, ..., 6=grandmaster

        [JsonPropertyName("members")]
        public List<CharacterTeamMember>? Members { get; set; }
    }

    /// <summary>
    /// Information about a team member's participation.
    /// </summary>
    public sealed class CharacterTeamMember
    {
        [JsonPropertyName("character")]
        public PlayerCharacterInfo? Character { get; set; }

        [JsonPropertyName("account")]
        public AccountInfo? Account { get; set; }

        [JsonPropertyName("raceGames")]
        public Dictionary<string, int>? RaceGames { get; set; }

        [JsonPropertyName("protossGamesPlayed")]
        public int? ProtossGamesPlayed { get; set; }

        [JsonPropertyName("terranGamesPlayed")]
        public int? TerranGamesPlayed { get; set; }

        [JsonPropertyName("zergGamesPlayed")]
        public int? ZergGamesPlayed { get; set; }

        public string? GetPrimaryRace()
        {
            var games = new Dictionary<string, int>();
            if (ProtossGamesPlayed > 0) games["PROTOSS"] = ProtossGamesPlayed.Value;
            if (TerranGamesPlayed > 0) games["TERRAN"] = TerranGamesPlayed.Value;
            if (ZergGamesPlayed > 0) games["ZERG"] = ZergGamesPlayed.Value;
            return games.OrderByDescending(x => x.Value).FirstOrDefault().Key;
        }
    }

    public sealed class PlayerCharacterInfo
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }
    }

    public sealed class AccountInfo
    {
        [JsonPropertyName("battleTag")]
        public string? BattleTag { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("tag")]
        public string? Tag { get; set; }

        [JsonPropertyName("discriminator")]
        public long? Discriminator { get; set; }
    }

    public sealed class LeagueInfo
    {
        [JsonPropertyName("type")]
        public int? Type { get; set; }  // 0=bronze, 1=silver, ..., 6=grandmaster

        [JsonPropertyName("queueType")]
        public int? QueueType { get; set; }

        [JsonPropertyName("teamType")]
        public int? TeamType { get; set; }
    }
}
