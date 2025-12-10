using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class BalanceReportsQuery
    {
        public Queue Queue { get; set; } = Queue.LOTV_1V1;
        public TeamArrangement TeamType { get; set; } = TeamArrangement.ARRANGED;
        public League League { get; set; } = League.MASTER;
        public LadderTier Tier { get; set; } = LadderTier.FIRST;
        public int Season { get; set; }
        public List<Region>? Regions { get; set; }
        public List<Race>? Races { get; set; }
        public List<bool>? CrossTier { get; set; }
        public int? FrameNumberMax { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>
            {
                new("queue", Queue.ToString()),
                new("teamType", TeamType.ToString()),
                new("league", League.ToString()),
                new("tier", Tier.ToString()),
                new("season", Season.ToString())
            };

            if (Regions?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("region", string.Join(",", Regions.Select(r => r.ToString()))));
            }

            if (Races?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("race", string.Join(",", Races.Select(r => r.ToString()))));
            }

            if (CrossTier?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("crossTier", string.Join(",", CrossTier.Select(b => b.ToString().ToLowerInvariant()))));
            }

            if (FrameNumberMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("frameNumberMax", FrameNumberMax.Value.ToString()));
            }

            return items.ToQueryString();
        }
    }
}
