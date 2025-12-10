using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class Group
    {
        [JsonPropertyName("characters")]
        public List<LadderDistinctCharacter> Characters { get; set; } = new();

        [JsonPropertyName("clans")]
        public List<Clan> Clans { get; set; } = new();

        [JsonPropertyName("proPlayers")]
        public List<LadderProPlayer> ProPlayers { get; set; } = new();

        [JsonPropertyName("accounts")]
        public List<Account> Accounts { get; set; } = new();
    }
}
