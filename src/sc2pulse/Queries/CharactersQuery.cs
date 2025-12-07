namespace Sc2Pulse.Queries
{
    public sealed class CharactersQuery
    {
        public string? Field { get; set; }
        public List<long>? CharacterId { get; set; }
        public List<int>? ClanId { get; set; }
        public List<long>? ProPlayerId { get; set; }
        public List<long>? AccountId { get; set; }
        public List<string>? ToonHandle { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();
            if (!string.IsNullOrEmpty(Field))
                items.Add(new KeyValuePair<string, string?>("field", Field));
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