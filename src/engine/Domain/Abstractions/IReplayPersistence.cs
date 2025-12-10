using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Abstractions
{
    public interface IReplayPersistence
    {
        Task SaveMatchAsync(MatchResult match);
        Task SaveBuildOrderAsync(string opponentTag, IReadOnlyList<BuildOrderStep> buildOrder);
        Task SaveMatchNoteAsync(string opponentTag, DateTime gameDate, string note);
    }
}
