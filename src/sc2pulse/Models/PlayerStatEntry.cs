using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    /// <summary>
    /// Full player statistics array from SC2Pulse API.
    /// The API returns an array of stat entries, each containing:
    /// - Overall stats (race: null)
    /// - Race-specific stats (race: TERRAN, PROTOSS, ZERG)
    /// - Different queue types (1v1, 2v2, 3v3, 4v4)
    /// - Solo and team variants
    /// </summary>
    public sealed class PlayerStatEntry
    {
        [JsonPropertyName("stats")]
        public StatSnapshot? Stats { get; set; }

        [JsonPropertyName("previousStats")]
        public StatSnapshot? PreviousStats { get; set; }

        [JsonPropertyName("currentStats")]
        public StatSnapshot? CurrentStats { get; set; }
    }

    /// <summary>
    /// A snapshot of player statistics at a point in time.
    /// </summary>
    public sealed class StatSnapshot
    {
        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("playerCharacterId")]
        public long? PlayerCharacterId { get; set; }

        [JsonPropertyName("queueType")]
        public int? QueueType { get; set; }  // 201=1v1, 202=2v2, 203=3v3, 204=4v4

        [JsonPropertyName("teamType")]
        public int? TeamType { get; set; }  // 0=solo, 1=team

        [JsonPropertyName("race")]
        public string? Race { get; set; }  // null=overall, "TERRAN", "PROTOSS", "ZERG"

        [JsonPropertyName("rating")]
        public int? Rating { get; set; }

        [JsonPropertyName("ratingMax")]
        public int? RatingMax { get; set; }

        [JsonPropertyName("league")]
        public int? League { get; set; }  // 0=bronze, 1=silver, 2=gold, 3=platinum, 4=diamond, 5=master, 6=grandmaster

        [JsonPropertyName("leagueMax")]
        public int? LeagueMax { get; set; }

        [JsonPropertyName("rank")]
        public int? Rank { get; set; }

        [JsonPropertyName("gamesPlayed")]
        public int? GamesPlayed { get; set; }
    }
}
