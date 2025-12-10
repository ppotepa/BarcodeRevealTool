using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public class MatchHistoryService : IMatchHistoryService
    {
        private readonly IReplayRepository _repository;

        public MatchHistoryService(IReplayRepository repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public IReadOnlyList<MatchResult> GetHistory(string yourTag, string opponentTag, int limit, string? opponentToon = null)
        {
            if (string.IsNullOrWhiteSpace(yourTag))
            {
                throw new ArgumentException("Your tag must be provided.", nameof(yourTag));
            }

            if (string.IsNullOrWhiteSpace(opponentTag))
            {
                throw new ArgumentException("Opponent tag must be provided.", nameof(opponentTag));
            }

            if (!string.IsNullOrWhiteSpace(opponentToon))
            {
                var matchesByToon = _repository.GetRecentMatchesByToon(opponentToon, limit);
                if (matchesByToon.Count > 0)
                {
                    return matchesByToon;
                }
            }

            return _repository.GetRecentMatches(yourTag, opponentTag, limit);
        }

        public string? GetLastKnownOpponentToon(string opponentTag)
        {
            if (string.IsNullOrWhiteSpace(opponentTag))
            {
                return null;
            }

            return _repository.GetLastKnownToon(opponentTag);
        }

        public MatchStatistics Analyze(IReadOnlyList<MatchResult> matches)
        {
            if (matches == null || matches.Count == 0)
            {
                return MatchStatistics.Empty;
            }

            var wins = matches.Count(m => m.YouWon);
            var losses = matches.Count - wins;
            var winRate = new WinRate(wins, losses);
            var lastGame = matches.Max(m => m.GameDate);

            return new MatchStatistics(matches.Count, winRate, lastGame);
        }
    }
}
