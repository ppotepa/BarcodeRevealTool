using BarcodeRevealTool.Replay;

namespace BarcodeRevealTool.Engine.Replay
{
    /// <summary>
    /// Manages lazy-loading of opponent build orders when transitioning to IN_GAME state.
    /// Decodes build order only when needed, then caches it in the database.
    /// Supports navigation through opponent history with Previous/Next replay.
    /// </summary>
    public interface IBuildOrderCacheManager
    {
        /// <summary>
        /// Load and decode opponent build order for the most recent replay against opponent.
        /// Only decodes if not already cached in database.
        /// Called when transitioning to IN_GAME state with opponent data available.
        /// </summary>
        Task<Queue<BuildOrderEntry>?> LoadOpponentBuildOrderAsync(
            string yourPlayerName,
            string opponentName,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync);

        /// <summary>
        /// Get build order for the next replay in opponent history.
        /// Returns null if no next replay exists.
        /// </summary>
        Task<Queue<BuildOrderEntry>?> GetNextOpponentBuildOrderAsync(
            string yourPlayerName,
            string opponentName,
            DateTime currentReplayDate,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync);

        /// <summary>
        /// Get build order for the previous replay in opponent history.
        /// Returns null if no previous replay exists.
        /// </summary>
        Task<Queue<BuildOrderEntry>?> GetPreviousOpponentBuildOrderAsync(
            string yourPlayerName,
            string opponentName,
            DateTime currentReplayDate,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync);

        /// <summary>
        /// Get current replay info for display purposes.
        /// </summary>
        ReplayRecord? GetCurrentReplayInfo(long replayId);
    }

    public class BuildOrderCacheManager : IBuildOrderCacheManager
    {
        private readonly IReplayQueryService _queryService;

        public BuildOrderCacheManager(IReplayQueryService queryService)
        {
            _queryService = queryService;
        }

        public async Task<Queue<BuildOrderEntry>?> LoadOpponentBuildOrderAsync(
            string yourPlayerName,
            string opponentName,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync)
        {
            try
            {
                // Find most recent replay against opponent
                var replayId = _queryService.GetMostRecentOpponentReplayId(yourPlayerName, opponentName);
                if (!replayId.HasValue)
                    return null;

                var replay = _queryService.GetReplayById(replayId.Value);
                if (replay == null)
                    return null;

                // Check if already cached in database
                if (replay.BuildOrderCached && replay.CachedAt.HasValue)
                {
                    // Load from database cache
                    return _queryService.GetBuildOrderEntries(replayId.Value);
                }

                // Not cached - decode from replay file (lazy-load)
                var buildOrder = await decodeReplayAsync(replay.ReplayFilePath);
                if (buildOrder != null && buildOrder.Count > 0)
                {
                    // Store in database for future use
                    _queryService.StoreBuildOrderEntries(replayId.Value, buildOrder);
                }

                return buildOrder;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderCacheManager] Error loading opponent build order: {ex.Message}");
                return null;
            }
        }

        public async Task<Queue<BuildOrderEntry>?> GetNextOpponentBuildOrderAsync(
            string yourPlayerName,
            string opponentName,
            DateTime currentReplayDate,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync)
        {
            try
            {
                // Find next replay chronologically
                var nextReplayId = _queryService.GetNextOpponentReplayId(yourPlayerName, opponentName, currentReplayDate);
                if (!nextReplayId.HasValue)
                    return null;

                return await LoadBuildOrderFromReplayIdAsync(nextReplayId.Value, decodeReplayAsync);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderCacheManager] Error loading next opponent build order: {ex.Message}");
                return null;
            }
        }

        public async Task<Queue<BuildOrderEntry>?> GetPreviousOpponentBuildOrderAsync(
            string yourPlayerName,
            string opponentName,
            DateTime currentReplayDate,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync)
        {
            try
            {
                // Find previous replay chronologically
                var prevReplayId = _queryService.GetPreviousOpponentReplayId(yourPlayerName, opponentName, currentReplayDate);
                if (!prevReplayId.HasValue)
                    return null;

                return await LoadBuildOrderFromReplayIdAsync(prevReplayId.Value, decodeReplayAsync);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderCacheManager] Error loading previous opponent build order: {ex.Message}");
                return null;
            }
        }

        public ReplayRecord? GetCurrentReplayInfo(long replayId)
        {
            try
            {
                return _queryService.GetReplayById(replayId);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderCacheManager] Error getting replay info: {ex.Message}");
                return null;
            }
        }

        private async Task<Queue<BuildOrderEntry>?> LoadBuildOrderFromReplayIdAsync(
            long replayId,
            Func<string, Task<Queue<BuildOrderEntry>?>> decodeReplayAsync)
        {
            var replay = _queryService.GetReplayById(replayId);
            if (replay == null)
                return null;

            // Check if already cached in database
            if (replay.BuildOrderCached && replay.CachedAt.HasValue)
            {
                // Load from database cache
                return _queryService.GetBuildOrderEntries(replayId);
            }

            // Not cached - decode from replay file (lazy-load)
            var buildOrder = await decodeReplayAsync(replay.ReplayFilePath);
            if (buildOrder != null && buildOrder.Count > 0)
            {
                // Store in database for future use
                _queryService.StoreBuildOrderEntries(replayId, buildOrder);
            }

            return buildOrder;
        }
    }
}
