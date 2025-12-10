using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public interface IBuildOrderService
    {
        IReadOnlyList<BuildOrderStep> GetRecentBuild(string opponentTag, int limit);
        BuildOrderPattern AnalyzePattern(string opponentTag, IReadOnlyList<BuildOrderStep> steps);
    }
}
