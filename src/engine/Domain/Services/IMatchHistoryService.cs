using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public interface IMatchHistoryService
    {
        IReadOnlyList<MatchResult> GetHistory(string yourTag, string opponentTag, int limit, string? opponentToon = null);
        string? GetLastKnownOpponentToon(string opponentTag);
        MatchStatistics Analyze(IReadOnlyList<MatchResult> matches);
    }
}
