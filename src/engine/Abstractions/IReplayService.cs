namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Abstraction for replay management operations.
    /// Allows Engine to work with any replay storage/retrieval implementation.
    /// </summary>
    public interface IReplayService
    {
        /// <summary>
        /// Callback invoked when cache operations complete (initialization or sync).
        /// Allows UI to refresh display after cache is ready.
        /// </summary>
        Action? OnCacheOperationComplete { get; set; }

        Task InitializeCacheAsync();
        Task SyncReplaysFromDiskAsync();
        /// <summary>
        /// Save a single replay to the database (no folder scan).
        /// Used when exiting a game to save the just-played replay.
        /// </summary>
        Task SaveReplayToDbAsync(string replayFilePath);

        /// <summary>
        /// Get match history against a specific opponent.
        /// Returns list of (opponentName, gameDate, map, yourRace, opponentRace, replayFileName, winner, replayFilePath)
        /// </summary>
        List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName, string? winner, string replayFilePath)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10);

        /// <summary>
        /// Get the most recent cached build order for a specific opponent by name.
        /// Searches replays where this opponent was played against.
        /// </summary>
        List<(double timeSeconds, string kind, string name)>?
            GetOpponentLastBuildOrder(string opponentName, int limit = 20);

        List<(string yourName, string opponentName, string yourRace, string opponentRace, DateTime gameDate, string map)>
            GetGamesByOpponentId(string yourPlayerId, string opponentPlayerId, int limit = 100);
    }
}
