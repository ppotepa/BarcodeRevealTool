using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderClanMemberEvents
    {
        [JsonPropertyName("characters")]
        public List<LadderDistinctCharacter> Characters { get; set; } = new();

        [JsonPropertyName("clans")]
        public List<Clan> Clans { get; set; } = new();

        [JsonPropertyName("events")]
        public List<ClanMemberEvent> Events { get; set; } = new();
    }
}
