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
    }
}
