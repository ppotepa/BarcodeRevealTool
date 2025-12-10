using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;
using System.Collections.Concurrent;

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

        public IReadOnlyList<MatchResult> GetRecentMatchesByToon(string opponentToon, int limit)
        {
            if (string.IsNullOrWhiteSpace(opponentToon))
            {
                return Array.Empty<MatchResult>();
            }

            return _matches.Values
                .SelectMany(m => m)
                .Where(m => string.Equals(m.OpponentToon, opponentToon, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.GameDate)
                .Take(limit)
                .ToList();
        }

        public string? GetLastKnownToon(string opponentTag)
        {
            if (!_matches.TryGetValue(opponentTag, out var list) || list.Count == 0)
            {
                return null;
            }

            return list
                .OrderByDescending(m => m.GameDate)
                .Select(m => m.OpponentToon)
                .FirstOrDefault(toon => !string.IsNullOrWhiteSpace(toon));
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

        public Task SaveMatchNoteAsync(string opponentTag, DateTime gameDate, string note)
        {
            if (string.IsNullOrWhiteSpace(note))
            {
                return Task.CompletedTask;
            }

            if (!_matches.TryGetValue(opponentTag, out var list))
            {
                return Task.CompletedTask;
            }

            lock (list)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i].GameDate == gameDate)
                    {
                        list[i] = list[i] with { Note = note };
                        break;
                    }
                }
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
