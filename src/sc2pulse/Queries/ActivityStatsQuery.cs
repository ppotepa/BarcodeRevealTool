using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class ActivityStatsQuery
    {
        public Queue Queue { get; set; } = Queue.LOTV_1V1;
        public TeamArrangement TeamType { get; set; } = TeamArrangement.ARRANGED;
        public List<Region>? Regions { get; set; }
        public List<League>? Leagues { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>
            {
                new("queue", Queue.ToString()),
                new("teamType", TeamType.ToString())
            };

            if (Regions?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("region", string.Join(",", Regions.Select(r => r.ToString()))));
            }

            if (Leagues?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("league", string.Join(",", Leagues.Select(l => l.ToString()))));
            }

            return items.ToQueryString();
        }
    }
}
