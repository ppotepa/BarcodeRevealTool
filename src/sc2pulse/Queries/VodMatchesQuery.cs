using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class VodMatchesQuery
    {
        public Race? Race { get; set; }
        public Race? RaceVersus { get; set; }
        public int? RatingMin { get; set; }
        public int? RatingMax { get; set; }
        public int? DurationMin { get; set; }
        public int? DurationMax { get; set; }
        public bool? IncludeSubOnly { get; set; }
        public int? MapId { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();

            if (Race.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("race", Race.Value.ToString()));
            }

            if (RaceVersus.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("raceVersus", RaceVersus.Value.ToString()));
            }

            if (RatingMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("ratingMin", RatingMin.Value.ToString()));
            }

            if (RatingMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("ratingMax", RatingMax.Value.ToString()));
            }

            if (DurationMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("durationMin", DurationMin.Value.ToString()));
            }

            if (DurationMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("durationMax", DurationMax.Value.ToString()));
            }

            if (IncludeSubOnly.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("includeSubOnly", IncludeSubOnly.Value.ToString().ToLowerInvariant()));
            }

            if (MapId.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("mapId", MapId.Value.ToString()));
            }

            if (!string.IsNullOrEmpty(Before))
            {
                items.Add(new KeyValuePair<string, string?>("before", Before));
            }

            if (!string.IsNullOrEmpty(After))
            {
                items.Add(new KeyValuePair<string, string?>("after", After));
            }

            return items.ToQueryString();
        }
    }
}
