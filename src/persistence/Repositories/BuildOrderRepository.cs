using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories
{
    /// <summary>
    /// Specialized repository for build order operations.
    /// </summary>
    public class BuildOrderRepository : Repository<BuildOrderEntity>, IBuildOrderRepository
    {
        public BuildOrderRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<IReadOnlyList<BuildOrderEntity>> GetRecentBuildOrdersAsync(string opponentTag, int limit = 20)
        {
            var all = await GetAllAsync(b =>
                b.OpponentTag?.Equals(opponentTag, StringComparison.OrdinalIgnoreCase) ?? false
            );

            return all
                .OrderByDescending(b => b.TimeSeconds)
                .Take(limit)
                .ToList();
        }

        public async Task<IReadOnlyList<BuildOrderEntity>> GetBuildOrdersByOpponentAsync(string opponentTag)
        {
            return await GetAllAsync(b =>
                b.OpponentTag?.Equals(opponentTag, StringComparison.OrdinalIgnoreCase) ?? false
            );
        }
    }
}
