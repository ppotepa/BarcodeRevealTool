using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderTeamMember
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("character")]
        public PlayerCharacter? Character { get; set; }

        [JsonPropertyName("teamSlot")]
        public int TeamSlot { get; set; }

        [JsonPropertyName("teamStateId")]
        public long? TeamStateId { get; set; }
    }
}
