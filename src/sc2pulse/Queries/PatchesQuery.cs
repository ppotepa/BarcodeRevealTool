namespace Sc2Pulse.Queries
{
    public sealed class PatchesQuery
    {
        public long? BuildMin { get; set; }

        public string ToQueryString()
        {
            if (!BuildMin.HasValue)
            {
                return string.Empty;
            }

            var items = new List<KeyValuePair<string, string?>>
            {
                new("buildMin", BuildMin.Value.ToString())
            };

            return items.ToQueryString();
        }
    }
}
