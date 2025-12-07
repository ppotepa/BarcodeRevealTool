using Sc2Pulse.Models;

namespace Sc2Pulse.Queries
{
    public sealed class CharacterTeamsQuery
    {
        public List<Queue>? Queue { get; set; }
        public List<int>? Season { get; set; }
        public List<Race>? Race { get; set; }
        public int? Limit { get; set; }
        public List<long>? CharacterId { get; set; }
        public List<int>? ClanId { get; set; }
        public List<long>? ProPlayerId { get; set; }
        public List<long>? AccountId { get; set; }
        public List<string>? ToonHandle { get; set; }

        public string ToQueryString()
        {
            var items = new List<KeyValuePair<string, string?>>();
            if (Queue?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("queue", string.Join(",", Queue.Select(q => q.ToString()))));
            if (Season?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("season", string.Join(",", Season)));
            if (Race?.Any() == true)
                items.Add(new KeyValuePair<string, string?>("race", string.Join(",", Race.Select(r => r.ToString()))));
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