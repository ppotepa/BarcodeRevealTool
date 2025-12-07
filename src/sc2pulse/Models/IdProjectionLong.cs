using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class IdProjectionLong
    {
        [JsonPropertyName("id")]
        public long Id { get; set; }
    }
}