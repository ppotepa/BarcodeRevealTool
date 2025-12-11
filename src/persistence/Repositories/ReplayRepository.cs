using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;
using SqlKata;
using SqlKata.Compilers;

namespace BarcodeRevealTool.Persistence.Repositories
{
    /// <summary>
    /// Specialized repository for replay file operations.
    /// </summary>
    public class ReplayRepository : Repository<ReplayFileEntity>, IReplayFileRepository
    {
        public ReplayRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<ReplayFileEntity>> GetReplaysWithPlayerAsync(string playerTag)
        {
            return await GetAllAsync(r =>
                (r.YourTag?.Contains(playerTag, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (r.OpponentTag?.Contains(playerTag, StringComparison.OrdinalIgnoreCase) ?? false)
            );
        }

        public async Task<IReadOnlyList<ReplayFileEntity>> GetRecentMatchesAsync(string opponentTag, int limit = 10)
        {
            var all = await GetAllAsync(r =>
                r.OpponentTag?.Equals(opponentTag, StringComparison.OrdinalIgnoreCase) ?? false
            );

            return all
                .OrderByDescending(r => r.GameDate)
                .Take(limit)
                .ToList();
        }

        public async Task<ReplayFileEntity?> GetByFilePathAsync(string replayFilePath)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(r =>
                r.ReplayFilePath?.Equals(replayFilePath, StringComparison.OrdinalIgnoreCase) ?? false
            );
        }

        public async Task<bool> ReplayExistsByPathAsync(string replayFilePath)
        {
            var replay = await GetByFilePathAsync(replayFilePath);
            return replay != null;
        }
    }
}
