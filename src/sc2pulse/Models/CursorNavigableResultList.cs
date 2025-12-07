using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class CursorNavigableResultList<T>
    {
        [JsonPropertyName("navigation")]
        public CursorNavigation? Navigation { get; set; }

        [JsonPropertyName("result")]
        public List<T> Result { get; set; } = new();
    }
}