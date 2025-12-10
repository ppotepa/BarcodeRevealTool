using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public class BuildOrderService : IBuildOrderService
    {
        private readonly IReplayRepository _repository;

        public BuildOrderService(IReplayRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<BuildOrderStep> GetRecentBuild(string opponentTag, int limit)
        {
            if (string.IsNullOrWhiteSpace(opponentTag))
            {
                throw new ArgumentException("Opponent tag must be provided.", nameof(opponentTag));
            }

            return _repository.GetRecentBuildOrder(opponentTag, limit);
        }

        public BuildOrderPattern AnalyzePattern(string opponentTag, IReadOnlyList<BuildOrderStep> steps)
        {
            var mostUsed = steps
                .GroupBy(s => s.Name)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "Unknown";

            return new BuildOrderPattern(opponentTag, steps, mostUsed, DateTime.UtcNow);
        }
    }
}
