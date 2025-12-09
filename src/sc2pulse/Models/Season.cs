using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    /// <summary>
    /// SC2 season information from SC2Pulse API.
    /// </summary>
    public sealed class Season
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("start")]
        public DateTime Start { get; set; }

        [JsonPropertyName("end")]
        public DateTime End { get; set; }

        [JsonPropertyName("id")]
        public long Id { get; set; }

        [JsonPropertyName("battlenetId")]
        public long BattlenetId { get; set; }

        [JsonPropertyName("region")]
        public string? Region { get; set; }  // KR, EU, US, CN

        public override string ToString() => $"Season {Number} ({Year}) - {Region} (ID: {Id})";
    }
}
