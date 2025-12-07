using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class CharacterMatchesQuery
    {
        public List<MatchKind>? Type { get; set; }
        public string? Before { get; set; }
        public string? After { get; set; }
        public int? Limit { get; set; }
        public List<long>? CharacterId { get; set; }
        public List<int>? ClanId { get; set; }
        public List<long>? ProPlayerId { get; set; }
        public List<long>? AccountId { get; set; }
        public List<string>? ToonHandle { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();
            if (Type?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("type", string.Join(",", Type.Select(t => t.ToString()))));
            if (!string.IsNullOrEmpty(Before))
                items.Add(new KeyValuePair<string, string?>("before", Before));
            if (!string.IsNullOrEmpty(After))
                items.Add(new KeyValuePair<string, string?>("after", After));
            if (Limit.HasValue)
                items.Add(new KeyValuePair<string, string?>("limit", Limit.Value.ToString()));
            if (CharacterId?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("characterId", string.Join(",", CharacterId)));
            if (ClanId?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("clanId", string.Join(",", ClanId)));
            if (ProPlayerId?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("proPlayerId", string.Join(",", ProPlayerId)));
            if (AccountId?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("accountId", string.Join(",", AccountId)));
            if (ToonHandle?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("toonHandle", string.Join(",", ToonHandle)));
            return items.ToQueryString();
        }
    }
}