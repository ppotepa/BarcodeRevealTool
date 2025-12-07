using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class PlayerCharacter
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("region")]
        public Region Region { get; set; }

        [JsonPropertyName("race")]
        public int? Race { get; set; }

        [JsonPropertyName("battleNetId")]
        public long? BattleNetId { get; set; }

        [JsonPropertyName("realm")]
        public int? Realm { get; set; }
    }
}