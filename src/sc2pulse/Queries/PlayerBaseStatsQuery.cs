using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class PlayerBaseStatsQuery
    {
        public Queue Queue { get; set; } = Queue.LOTV_1V1;
        public TeamArrangement TeamType { get; set; } = TeamArrangement.ARRANGED;

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>
            {
                new("queue", Queue.ToString()),
                new("teamType", TeamType.ToString())
            };

            return items.ToQueryString();
        }
    }
}
