using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Abstractions
{
    /// <summary>
    /// Manages cached replay inventory and provides validation/status information.
    /// </summary>
    public interface ICacheManager
    {
        Task InitializeAsync();
        Task SyncFromDiskAsync(string replayFolder, bool recursive);
        Task SyncMissingReplaysAsync(string replayFolder, bool recursive);
        Task SyncRecentReplayAsync(string replayFolder);
        CacheStatistics GetStatistics();
        bool IsCacheValid();
        bool IsCacheEmpty();
        bool WasFullSyncJustCompleted { get; }
        void ResetFullSyncFlag();
        IReadOnlyList<string> GetMissingReplayFiles(string[] diskFiles);
    }
}
