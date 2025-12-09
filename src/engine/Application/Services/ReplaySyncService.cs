using System;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Application.Services
{
    public class ReplaySyncService : IReplaySyncService
    {
        private readonly AppSettings _settings;
        private readonly IReplayPersistence _persistence;
        private readonly IReplayRepository _repository;

        public ReplaySyncService(AppSettings settings, IReplayPersistence persistence, IReplayRepository repository)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            // Seed the cache with a single placeholder match so the UI has data.
            if (_repository.GetRecentMatches(_settings.User.BattleTag, "opponent#1", 1).Count == 0)
            {
                var match = new MatchResult(
                    "opponent#1",
                    DateTime.UtcNow.AddDays(-1),
                    "Dragon Scales",
                    "Protoss",
                    "Terran",
                    true);

                await _persistence.SaveMatchAsync(match);

                var build = new[]
                {
                    new BuildOrderStep(17, "Building", "Barracks"),
                    new BuildOrderStep(35, "Building", "Factory")
                };
                await _persistence.SaveBuildOrderAsync(match.OpponentTag, build);
            }
        }

        public Task SyncAsync(CancellationToken cancellationToken)
        {
            // Real implementation would scan the replay folder.
            // For now we simply update the last sync timestamp via a no-op write.
            return InitializeAsync(cancellationToken);
        }

        public CacheStatistics GetStatistics() => _repository.GetCacheStatistics();
    }
}
