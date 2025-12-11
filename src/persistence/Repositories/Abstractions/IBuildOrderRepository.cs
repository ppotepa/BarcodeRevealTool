using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories.Abstractions
{
    /// <summary>
    /// Specialized repository interface for build order queries.
    /// </summary>
    public interface IBuildOrderRepository : IRepository<BuildOrderEntity>
    {
        /// <summary>Get recent build orders for an opponent.</summary>
        Task<IReadOnlyList<BuildOrderEntity>> GetRecentBuildOrdersAsync(string opponentTag, int limit = 20);

        /// <summary>Get all build orders for an opponent.</summary>
        Task<IReadOnlyList<BuildOrderEntity>> GetBuildOrdersByOpponentAsync(string opponentTag);
    }
}
