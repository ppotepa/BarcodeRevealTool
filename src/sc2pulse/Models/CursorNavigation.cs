using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class CursorNavigation
    {
        [JsonPropertyName("before")]
        public string? Before { get; set; }

        [JsonPropertyName("after")]
        public string? After { get; set; }
    }
}