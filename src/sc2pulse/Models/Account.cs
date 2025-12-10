using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class Account
    {
        [JsonPropertyName("battleTag")]
        public string? BattleTag { get; set; }

        [JsonPropertyName("id")]
        public long? Id { get; set; }

        [JsonPropertyName("partition")]
        public string Partition { get; set; } = "GLOBAL";

        [JsonPropertyName("hidden")]
        public bool? Hidden { get; set; }
    }
}
