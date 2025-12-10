using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Presentation
{
    public interface IMatchHistoryRenderer
    {
        void RenderMatchHistory(IReadOnlyList<MatchResult> matches, MatchStatistics statistics);
        void RenderOpponentProfile(OpponentProfile profile);
    }
}
