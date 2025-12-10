using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class ClansQuery
    {
        public Region? Region { get; set; }
        public int? ActiveMembersMin { get; set; }
        public int? ActiveMembersMax { get; set; }
        public double? GamesPerActiveMemberPerDayMin { get; set; }
        public double? GamesPerActiveMemberPerDayMax { get; set; }
        public int? AvgRatingMin { get; set; }
        public int? AvgRatingMax { get; set; }
        public string? Sort { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();

            if (Region.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("region", Region.Value.ToString()));
            }

            if (ActiveMembersMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("activeMembersMin", ActiveMembersMin.Value.ToString()));
            }

            if (ActiveMembersMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("activeMembersMax", ActiveMembersMax.Value.ToString()));
            }

            if (GamesPerActiveMemberPerDayMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("gamesPerActiveMemberPerDayMin", GamesPerActiveMemberPerDayMin.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)));
            }

            if (GamesPerActiveMemberPerDayMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("gamesPerActiveMemberPerDayMax", GamesPerActiveMemberPerDayMax.Value.ToString("G", System.Globalization.CultureInfo.InvariantCulture)));
            }

            if (AvgRatingMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("avgRatingMin", AvgRatingMin.Value.ToString()));
            }

            if (AvgRatingMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("avgRatingMax", AvgRatingMax.Value.ToString()));
            }

            if (!string.IsNullOrEmpty(Sort))
            {
                items.Add(new KeyValuePair<string, string?>("sort", Sort));
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
