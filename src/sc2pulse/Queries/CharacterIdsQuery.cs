using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class CharacterIdsQuery
    {
        public string Name { get; set; } = string.Empty;
        public string? Region { get; set; }
        public List<Queue>? Queue { get; set; }
        public List<int>? Season { get; set; }
        public bool? CaseSensitive { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();
            if (!string.IsNullOrEmpty(Name))
                items.Add(new KeyValuePair<string, string?>("name", Name));
            if (!string.IsNullOrEmpty(Region))
                items.Add(new KeyValuePair<string, string?>("region", Region));
            if (Queue?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("queue", string.Join(",", Queue.Select(q => q.ToString()))));
            if (Season?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("season", string.Join(",", Season)));
            if (CaseSensitive.HasValue)
                items.Add(new KeyValuePair<string, string?>("caseSensitive", CaseSensitive.Value.ToString().ToLowerInvariant()));
            return items.ToQueryString();
        }
    }
}