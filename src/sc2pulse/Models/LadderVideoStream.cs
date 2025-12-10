using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderVideoStream
    {
        [JsonPropertyName("stream")]
        public VideoStream? Stream { get; set; }

        [JsonPropertyName("proPlayer")]
        public LadderProPlayer? ProPlayer { get; set; }

        [JsonPropertyName("team")]
        public LadderTeam? Team { get; set; }

        [JsonPropertyName("featured")]
        public string? Featured { get; set; }
    }
}
