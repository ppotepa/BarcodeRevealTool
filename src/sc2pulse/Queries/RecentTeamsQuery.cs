using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class RecentTeamsQuery
    {
        public Queue Queue { get; set; } = Queue.LOTV_1V1;
        public League League { get; set; } = League.DIAMOND;
        public Region? Region { get; set; }
        public Race? Race { get; set; }
        public int? WinsMin { get; set; }
        public int? WinsMax { get; set; }
        public int? RatingMin { get; set; }
        public int? RatingMax { get; set; }
        public int? Limit { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>
            {
                new("queue", Queue.ToString()),
                new("league", League.ToString())
            };

            if (Region.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("region", Region.Value.ToString()));
            }

            if (Race.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("race", Race.Value.ToString()));
            }

            if (WinsMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("winsMin", WinsMin.Value.ToString()));
            }

            if (WinsMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("winsMax", WinsMax.Value.ToString()));
            }

            if (RatingMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("ratingMin", RatingMin.Value.ToString()));
            }

            if (RatingMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("ratingMax", RatingMax.Value.ToString()));
            }

            if (Limit.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("limit", Limit.Value.ToString()));
            }

            return items.ToQueryString();
        }
    }
}
