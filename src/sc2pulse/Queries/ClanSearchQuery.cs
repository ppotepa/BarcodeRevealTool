namespace Sc2Pulse.Queries
{
    public sealed class ClanSearchQuery
    {
        public string Query { get; set; } = string.Empty;

        public string ToQueryString()
        {
            if (string.IsNullOrWhiteSpace(Query))
            {
                throw new InvalidOperationException("Query must be provided.");
            }

            var items = new List<KeyValuePair<string, string?>>
            {
                new("query", Query)
            };

            return items.ToQueryString();
        }
    }
}
