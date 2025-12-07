namespace Sc2Pulse.Queries
{
    public sealed class CharacterSuggestionsQuery
    {
        public string Query { get; set; } = string.Empty;

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();
            if (!string.IsNullOrEmpty(Query))
                items.Add(new KeyValuePair<string, string?>("query", Query));
            return items.ToQueryString();
        }
    }
}