using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories.Abstractions
{
    /// <summary>
    /// Specialized repository interface for replay file queries.
    /// Extends the generic repository with replay-specific operations.
    /// </summary>
    public interface IReplayFileRepository : IRepository<ReplayFileEntity>
    {
        /// <summary>Get all replays involving a specific player opponent (pagination-capable).</summary>
        Task<IReadOnlyList<ReplayFileEntity>> GetReplaysWithPlayerAsync(string playerTag);

        /// <summary>Get recent matches against a specific opponent.</summary>
        Task<IReadOnlyList<ReplayFileEntity>> GetRecentMatchesAsync(string opponentTag, int limit = 10);

        /// <summary>Get replay by file path.</summary>
        Task<ReplayFileEntity?> GetByFilePathAsync(string replayFilePath);

        /// <summary>Check if replay exists by file path.</summary>
        Task<bool> ReplayExistsByPathAsync(string replayFilePath);
    }
}
