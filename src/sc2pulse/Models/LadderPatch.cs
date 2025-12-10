using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderPatch
    {
        [JsonPropertyName("patch")]
        public Patch Patch { get; set; } = new Patch();

        [JsonPropertyName("releases")]
        public Dictionary<string, DateTime>? Releases { get; set; }
    }
}
