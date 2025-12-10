using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderTeamMember
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("character")]
        public PlayerCharacter? Character { get; set; }

        [JsonPropertyName("account")]
        public Account? Account { get; set; }

        [JsonPropertyName("clan")]
        public Clan? Clan { get; set; }

        [JsonPropertyName("teamSlot")]
        public int TeamSlot { get; set; }

        [JsonPropertyName("teamStateId")]
        public long? TeamStateId { get; set; }

        [JsonPropertyName("proId")]
        public long? ProId { get; set; }

        [JsonPropertyName("proNickname")]
        public string? ProNickname { get; set; }

        [JsonPropertyName("proTeam")]
        public string? ProTeam { get; set; }

        [JsonPropertyName("proPlayer")]
        public LadderProPlayer? ProPlayer { get; set; }

        [JsonPropertyName("terranGamesPlayed")]
        public int? TerranGamesPlayed { get; set; }

        [JsonPropertyName("protossGamesPlayed")]
        public int? ProtossGamesPlayed { get; set; }

        [JsonPropertyName("zergGamesPlayed")]
        public int? ZergGamesPlayed { get; set; }

        [JsonPropertyName("randomGamesPlayed")]
        public int? RandomGamesPlayed { get; set; }

        [JsonPropertyName("raceGames")]
        public Dictionary<string, int>? RaceGames { get; set; }

        [JsonPropertyName("restrictions")]
        public bool? Restrictions { get; set; }

        public string? GetPrimaryRace()
        {
            var raceTotals = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (ProtossGamesPlayed.HasValue)
            {
                raceTotals["PROTOSS"] = ProtossGamesPlayed.Value;
            }

            if (TerranGamesPlayed.HasValue)
            {
                raceTotals["TERRAN"] = TerranGamesPlayed.Value;
            }

            if (ZergGamesPlayed.HasValue)
            {
                raceTotals["ZERG"] = ZergGamesPlayed.Value;
            }

            if (RandomGamesPlayed.HasValue)
            {
                raceTotals["RANDOM"] = RandomGamesPlayed.Value;
            }

            if (raceTotals.Count == 0 && RaceGames != null)
            {
                foreach (var kvp in RaceGames)
                {
                    raceTotals[kvp.Key] = kvp.Value;
                }
            }

            return raceTotals.OrderByDescending(r => r.Value).FirstOrDefault().Key;
        }
    }
}
