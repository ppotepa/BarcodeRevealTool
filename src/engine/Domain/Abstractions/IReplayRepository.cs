using System.Collections.Generic;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Abstractions
{
    public interface IReplayRepository
    {
        IReadOnlyList<MatchResult> GetRecentMatches(string yourTag, string opponentTag, int limit);
        IReadOnlyList<BuildOrderStep> GetRecentBuildOrder(string opponentTag, int limit);
        CacheStatistics GetCacheStatistics();
    }
}
