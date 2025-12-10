using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class TeamHistoriesQuery
    {
        public List<TeamHistoryMetric> HistoryFields { get; set; } = new();
        public List<TeamHistoryStaticField>? StaticFields { get; set; }
        public TeamHistoryGroupBy GroupBy { get; set; } = TeamHistoryGroupBy.TEAM;
        public DateTimeOffset? From { get; set; }
        public DateTimeOffset? To { get; set; }
        public List<long>? TeamIds { get; set; }
        public List<string>? TeamLegacyUids { get; set; }
        public int? SeasonMin { get; set; }
        public int? SeasonMax { get; set; }

        public string ToQueryString()
        {
            if (HistoryFields.Count == 0)
            {
                throw new InvalidOperationException("At least one history field must be provided.");
            }

            var items = new List<KeyValuePair<string, string?>>
            {
                new("history", string.Join(",", HistoryFields.Select(h => h.ToString())))
            };

            if (StaticFields?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("static", string.Join(",", StaticFields.Select(s => s.ToString()))));
            }

            items.Add(new KeyValuePair<string, string?>("groupBy", GroupBy.ToString()));

            if (From.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("from", From.Value.ToString("O")));
            }

            if (To.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("to", To.Value.ToString("O")));
            }

            if (TeamIds?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("teamId", string.Join(",", TeamIds)));
            }

            if (TeamLegacyUids?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("teamLegacyUid", string.Join(",", TeamLegacyUids)));
            }

            if (SeasonMin.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("seasonMin", SeasonMin.Value.ToString()));
            }

            if (SeasonMax.HasValue)
            {
                items.Add(new KeyValuePair<string, string?>("seasonMax", SeasonMax.Value.ToString()));
            }

            return items.ToQueryString();
        }
    }
}
