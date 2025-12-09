using System;
using System.Collections.Generic;
using System.Linq;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public class OpponentProfileService : IOpponentProfileService
    {
        private readonly IMatchHistoryService _matchHistoryService;
        private readonly IBuildOrderService _buildOrderService;

        public OpponentProfileService(
            IMatchHistoryService matchHistoryService,
            IBuildOrderService buildOrderService)
        {
            _matchHistoryService = matchHistoryService;
            _buildOrderService = buildOrderService;
        }

        public OpponentProfile BuildProfile(string yourTag, string opponentTag)
        {
            var history = _matchHistoryService.GetHistory(yourTag, opponentTag, 10);
            var stats = _matchHistoryService.Analyze(history);
            var build = _buildOrderService.GetRecentBuild(opponentTag, 20);
            var pattern = _buildOrderService.AnalyzePattern(opponentTag, build);

            var preferredRaces = new PreferredRaces(
                history.GroupBy(m => m.OpponentRace)
                    .OrderByDescending(g => g.Count())
                    .Select(g => g.Key)
                    .FirstOrDefault() ?? "Unknown");

            var favoriteMaps = history
                .GroupBy(m => m.Map)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => g.Key)
                .ToList();

            var lastPlayed = stats.LastGame ?? DateTime.MinValue;

            return new OpponentProfile(
                opponentTag,
                stats.WinRate,
                preferredRaces,
                favoriteMaps,
                pattern,
                lastPlayed);
        }
    }
}
