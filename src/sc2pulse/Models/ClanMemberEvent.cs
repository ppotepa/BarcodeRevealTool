using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class ClanMemberEvent
    {
        [JsonPropertyName("playerCharacterId")]
        public long PlayerCharacterId { get; set; }

        [JsonPropertyName("clanId")]
        public int ClanId { get; set; }

        [JsonPropertyName("type")]
        public ClanEventType Type { get; set; }

        [JsonPropertyName("created")]
        public DateTime Created { get; set; }

        [JsonPropertyName("secondsSincePrevious")]
        public int? SecondsSincePrevious { get; set; }
    }
}
