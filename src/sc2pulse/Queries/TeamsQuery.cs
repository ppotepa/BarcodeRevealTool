namespace Sc2Pulse.Queries
{
    public sealed class TeamsQuery
    {
        public string? Field { get; set; }
        public List<long>? TeamIds { get; set; }
        public List<string>? TeamLegacyUids { get; set; }
        public int? SeasonMin { get; set; }
        public int? SeasonMax { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();

            if (!string.IsNullOrEmpty(Field))
            {
                items.Add(new KeyValuePair<string, string?>("field", Field));
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
