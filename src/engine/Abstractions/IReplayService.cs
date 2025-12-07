namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Abstraction for replay management operations.
    /// Allows Engine to work with any replay storage/retrieval implementation.
    /// </summary>
    public interface IReplayService
    {
        Task InitializeCacheAsync();
        Task SyncReplaysFromDiskAsync();
        /// <summary>
        /// Save a single replay to the database (no folder scan).
        /// Used when exiting a game to save the just-played replay.
        /// </summary>
        Task SaveReplayToDbAsync(string replayFilePath);
    }
}
