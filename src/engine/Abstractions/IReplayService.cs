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
    }
}
