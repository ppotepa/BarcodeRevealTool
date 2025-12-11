namespace BarcodeRevealTool.Persistence.Repositories.Abstractions
{
    /// <summary>
    /// Unit of Work interface for coordinating multiple repository operations.
    /// Provides centralized access to all entity repositories across the entire application.
    /// All database operations should flow through this interface for consistency and auditability.
    /// </summary>
    public interface IUnitOfWork : IDisposable
    {
        // Debug & Tracking Repositories
        IRepository<Entities.DebugSessionEntity> DebugSessions { get; }
        IRepository<Entities.LobbyFileEntity> LobbyFiles { get; }
        IRepository<Entities.DebugSessionEventEntity> DebugSessionEvents { get; }
        IRepository<Entities.ConfigHistoryEntity> ConfigHistory { get; }

        // Replay & Game Data Repositories
        IReplayFileRepository Replays { get; }
        IBuildOrderRepository BuildOrders { get; }
        IUserAccountRepository UserAccounts { get; }

        /// <summary>Save all pending changes to the database atomically.</summary>
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
