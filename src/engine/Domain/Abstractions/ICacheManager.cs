using System.Collections.Generic;
using System.Threading.Tasks;
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
        CacheStatistics GetStatistics();
        bool IsCacheValid();
        IReadOnlyList<string> GetMissingReplayFiles(string[] diskFiles);
    }
}
