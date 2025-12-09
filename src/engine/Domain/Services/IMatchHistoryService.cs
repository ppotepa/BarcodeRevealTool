using System.Collections.Generic;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public interface IMatchHistoryService
    {
        IReadOnlyList<MatchResult> GetHistory(string yourTag, string opponentTag, int limit);
        MatchStatistics Analyze(IReadOnlyList<MatchResult> matches);
    }
}
