using System.Text.Json.Serialization;

namespace Sc2Pulse.Models
{
    public sealed class LocaleInfo
    {
        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("country")]
        public string? Country { get; set; }

        [JsonPropertyName("variant")]
        public string? Variant { get; set; }

        [JsonPropertyName("script")]
        public string? Script { get; set; }

        [JsonPropertyName("unicodeLocaleAttributes")]
        public List<string>? UnicodeLocaleAttributes { get; set; }

        [JsonPropertyName("unicodeLocaleKeys")]
        public List<string>? UnicodeLocaleKeys { get; set; }

        [JsonPropertyName("displayLanguage")]
        public string? DisplayLanguage { get; set; }

        [JsonPropertyName("displayScript")]
        public string? DisplayScript { get; set; }

        [JsonPropertyName("displayCountry")]
        public string? DisplayCountry { get; set; }

        [JsonPropertyName("displayVariant")]
        public string? DisplayVariant { get; set; }

        [JsonPropertyName("extensionKeys")]
        public List<string>? ExtensionKeys { get; set; }

        [JsonPropertyName("iso3Language")]
        public string? Iso3Language { get; set; }

        [JsonPropertyName("iso3Country")]
        public string? Iso3Country { get; set; }
    }
}
