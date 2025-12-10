namespace Sc2Pulse.Queries
{
    public sealed class ClanHistoriesQuery
    {
        public List<long>? CharacterIds { get; set; }
        public List<int>? ClanIds { get; set; }
        public List<long>? ProPlayerIds { get; set; }
        public List<long>? AccountIds { get; set; }
        public List<string>? ToonHandles { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();

            if (CharacterIds?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("characterId", string.Join(",", CharacterIds)));
            }

            if (ClanIds?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("clanId", string.Join(",", ClanIds)));
            }

            if (ProPlayerIds?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("proPlayerId", string.Join(",", ProPlayerIds)));
            }

            if (AccountIds?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("accountId", string.Join(",", AccountIds)));
            }

            if (ToonHandles?.Any() == true)
            {
                items.Add(new KeyValuePair<string, string?>("toonHandle", string.Join(",", ToonHandles)));
            }

            return items.ToQueryString();
        }
    }
}
