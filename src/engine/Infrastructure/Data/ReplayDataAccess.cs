using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Infrastructure.Data
{
    /// <summary>
    /// Very small in-memory backing store used for prototyping.
    /// </summary>
    public class ReplayDataAccess : IReplayRepository, IReplayPersistence
    {
        private readonly ConcurrentDictionary<string, List<MatchResult>> _matches = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<BuildOrderStep>> _buildOrders = new(StringComparer.OrdinalIgnoreCase);
        private DateTime _lastSync = DateTime.MinValue;

        public IReadOnlyList<MatchResult> GetRecentMatches(string yourTag, string opponentTag, int limit)
        {
            if (!_matches.TryGetValue(opponentTag, out var list))
            {
                return Array.Empty<MatchResult>();
            }

            return list
                .Where(m => string.Equals(m.OpponentTag, opponentTag, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.GameDate)
                .Take(limit)
                .ToList();
        }

        public IReadOnlyList<BuildOrderStep> GetRecentBuildOrder(string opponentTag, int limit)
        {
            if (!_buildOrders.TryGetValue(opponentTag, out var steps))
            {
                return Array.Empty<BuildOrderStep>();
            }

            return steps
                .OrderByDescending(s => s.TimeSeconds)
                .Take(limit)
                .ToList();
        }

        public CacheStatistics GetCacheStatistics()
        {
            var matchCount = _matches.Values.Sum(list => list.Count);
            var buildCount = _buildOrders.Values.Sum(list => list.Count);
            return new CacheStatistics(matchCount, buildCount, _lastSync);
        }

        public Task SaveMatchAsync(MatchResult match)
        {
            var list = _matches.GetOrAdd(match.OpponentTag, _ => new List<MatchResult>());
            lock (list)
            {
                list.Add(match);
            }

            _lastSync = DateTime.UtcNow;
            return Task.CompletedTask;
        }

        public Task SaveBuildOrderAsync(string opponentTag, IReadOnlyList<BuildOrderStep> buildOrder)
        {
            var list = _buildOrders.GetOrAdd(opponentTag, _ => new List<BuildOrderStep>());
            lock (list)
            {
                list.Clear();
                list.AddRange(buildOrder);
            }

            _lastSync = DateTime.UtcNow;
            return Task.CompletedTask;
        }
    }
}
