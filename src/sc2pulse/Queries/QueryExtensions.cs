using System.Text;

namespace Sc2Pulse.Queries
{
    internal static class QueryExtensions
    {
        public static string ToQueryString(this IEnumerable<KeyValuePair<string, string?>>? pairs)
        {
            if (pairs == null) return string.Empty;
            var filtered = pairs.Where(p => !string.IsNullOrEmpty(p.Value)).ToList();
            if (!filtered.Any()) return string.Empty;

            var sb = new StringBuilder("?");
            foreach (var kv in filtered)
            {
                if (sb.Length > 1) sb.Append("&");
                sb.Append(Uri.EscapeDataString(kv.Key));
                sb.Append("=");
                sb.Append(Uri.EscapeDataString(kv.Value!));
            }
            return sb.ToString();
        }
    }
}