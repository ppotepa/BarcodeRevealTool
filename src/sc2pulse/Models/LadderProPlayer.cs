using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LadderProPlayer
    {
        [JsonPropertyName("proPlayer")]
        public ProPlayer? ProPlayer { get; set; }

        [JsonPropertyName("proTeam")]
        public ProTeam? ProTeam { get; set; }

        [JsonPropertyName("links")]
        public List<SocialMediaLink> Links { get; set; } = new();
    }
}
