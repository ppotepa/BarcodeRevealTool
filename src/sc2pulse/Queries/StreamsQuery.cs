using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class StreamsQuery
    {
        public List<LinkType>? Services { get; set; }
        public bool? IdentifiedOnly { get; set; }
        public bool? Lax { get; set; }
        public List<MatchKind>? TeamFormats { get; set; }
        public List<Race>? Races { get; set; }
        public List<string>? Languages { get; set; }
        public int? RatingMin { get; set; }
        public int? RatingMax { get; set; }
        public string? Sort { get; set; }
        public int? Limit { get; set; }
        public int? LimitPlayer { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();

            if (Services?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("service", string.Join(",", Services.Select(s => s.ToString()))));
            }

            if (IdentifiedOnly.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("identifiedOnly", IdentifiedOnly.Value.ToString().ToLowerInvariant()));
            }

            if (Lax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("lax", Lax.Value.ToString().ToLowerInvariant()));
            }

            if (TeamFormats?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("teamFormat", string.Join(",", TeamFormats.Select(f => f.ToString()))));
            }

            if (Races?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("race", string.Join(",", Races.Select(r => r.ToString()))));
            }

            if (Languages?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("language", string.Join(",", Languages)));
            }

            if (RatingMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("ratingMin", RatingMin.Value.ToString()));
            }

            if (RatingMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("ratingMax", RatingMax.Value.ToString()));
            }

            if (!string.IsNullOrEmpty(Sort))
            {
                items.Add(new KeyValuePair<string, string?>("sort", Sort));
            }

            if (Limit.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("limit", Limit.Value.ToString()));
            }

            if (LimitPlayer.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("limitPlayer", LimitPlayer.Value.ToString()));
            }

            return items.ToQueryString();
        }
    }
}
