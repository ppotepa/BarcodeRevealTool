using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Abstractions
{
    public interface IReplayRepository
    {
        IReadOnlyList<MatchResult> GetRecentMatches(string yourTag, string opponentTag, int limit);
        IReadOnlyList<MatchResult> GetRecentMatchesByToon(string opponentToon, int limit);
        string? GetLastKnownToon(string opponentTag);
        IReadOnlyList<BuildOrderStep> GetRecentBuildOrder(string opponentTag, int limit);
        CacheStatistics GetCacheStatistics();
    }
}
