namespace Sc2Pulse.Queries
{
    public sealed class LastTeamsQuery
    {
        public List<string> TeamLegacyUids { get; set; } = new();

        public string ToQueryString()
        {
            if (TeamLegacyUids.Count == 0)
            {
                throw new InvalidOperationException("At least one team legacy UID must be provided.");
            }

            var items = new List<KeyValuePair<string, string?>>
            {
                new("teamLegacyUid", string.Join(",", TeamLegacyUids))
            };

            return items.ToQueryString();
        }
    }
}
