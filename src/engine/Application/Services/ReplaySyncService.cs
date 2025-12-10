using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;
using Serilog;

namespace BarcodeRevealTool.Engine.Application.Services
{
    public class ReplaySyncService : IReplaySyncService
    {
        private readonly AppSettings _settings;
        private readonly ICacheManager _cacheManager;
        private readonly ILogger _logger = Log.ForContext<ReplaySyncService>();

        public ReplaySyncService(AppSettings settings, ICacheManager cacheManager)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _cacheManager = cacheManager ?? throw new ArgumentNullException(nameof(cacheManager));
        }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            await _cacheManager.InitializeAsync().ConfigureAwait(false);

            // Only scan for new replays if:
            // 1. A full sync was just completed (cache was empty and we just initialized it)
            // 2. We explicitly tell it to scan
            // On normal startup with existing cache, don't scan
            if (_cacheManager.WasFullSyncJustCompleted)
            {
                _logger.Information("Full cache sync was just completed. Skipping startup scan as replays were already processed.");
                _cacheManager.ResetFullSyncFlag();
            }
            else
            {
                _logger.Information("Cache initialized. Ready to monitor for new replays during gameplay.");
            }
        }

        public Task SyncAsync(CancellationToken cancellationToken) =>
            ScanForRecentReplayAsync(cancellationToken);

        public Task SyncMissingAsync(CancellationToken cancellationToken) =>
            ScanForNewReplaysAsync("manual", cancellationToken);

        public CacheStatistics GetStatistics() => _cacheManager.GetStatistics();

        private async Task ScanForRecentReplayAsync(CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var replayFolder = _settings.Replays?.Folder;
                if (string.IsNullOrWhiteSpace(replayFolder))
                {
                    _logger.Warning("Replay folder not configured. Skipping recent replay sync");
                    return;
                }

                _logger.Information("Syncing most recent replay from {ReplayFolder} (game just finished)", replayFolder);
                await _cacheManager.SyncRecentReplayAsync(replayFolder).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to synchronize recent replay after game completion");
            }
        }

        private async Task ScanForNewReplaysAsync(string context, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                var replayFolder = _settings.Replays?.Folder;
                if (string.IsNullOrWhiteSpace(replayFolder))
                {
                    _logger.Warning("Replay folder not configured. Skipping replay sync during {Context}", context);
                    return;
                }

                _logger.Information("Scanning {ReplayFolder} for new replays ({Context})", replayFolder, context);
                await _cacheManager.SyncMissingReplaysAsync(replayFolder, _settings.Replays?.Recursive ?? true).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to synchronize replays during {Context}", context);
            }
        }
    }
}
