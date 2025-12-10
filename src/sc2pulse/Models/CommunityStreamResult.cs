using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class CommunityStreamResult
    {
        [JsonPropertyName("streams")]
        public List<LadderVideoStream> Streams { get; set; } = new();

        [JsonPropertyName("errors")]
        public List<LinkType>? Errors { get; set; }
    }
}
