using Serilog;
using SqlKata.Compilers;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories
{
    /// <summary>
    /// Unit of Work implementation coordinating all repository operations across the application.
    /// Provides centralized access to all entity repositories through a single interface.
    /// All database operations flow through this single coordinator.
    /// </summary>
    public class UnitOfWork : IUnitOfWork
    {
        private readonly string _connectionString;
        private readonly SqliteCompiler _compiler;
        private readonly ILogger _logger;

        // Debug & Tracking Repositories
        private Repository<DebugSessionEntity>? _debugSessions;
        private Repository<LobbyFileEntity>? _lobbyFiles;
        private Repository<DebugSessionEventEntity>? _debugSessionEvents;
        private Repository<ConfigHistoryEntity>? _configHistory;

        // Replay & Game Data Repositories
        private ReplayRepository? _replays;
        private BuildOrderRepository? _buildOrders;
        private UserAccountRepository? _userAccounts;

        public UnitOfWork(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
            _compiler = new SqliteCompiler();
            _logger = Log.ForContext<UnitOfWork>();
        }

        // === Debug & Tracking Repositories ===

        /// <summary>Repository for DebugSession entities (application run tracking).</summary>
        public IRepository<DebugSessionEntity> DebugSessions =>
            _debugSessions ??= new Repository<DebugSessionEntity>(_connectionString, _compiler);

        /// <summary>Repository for LobbyFile entities (binary lobby file storage).</summary>
        public IRepository<LobbyFileEntity> LobbyFiles =>
            _lobbyFiles ??= new Repository<LobbyFileEntity>(_connectionString, _compiler);

        /// <summary>Repository for DebugSessionEvent entities (event logging).</summary>
        public IRepository<DebugSessionEventEntity> DebugSessionEvents =>
            _debugSessionEvents ??= new Repository<DebugSessionEventEntity>(_connectionString, _compiler);

        /// <summary>Repository for ConfigHistory entities (configuration change tracking).</summary>
        public IRepository<ConfigHistoryEntity> ConfigHistory =>
            _configHistory ??= new Repository<ConfigHistoryEntity>(_connectionString, _compiler);

        // === Replay & Game Data Repositories ===

        /// <summary>Repository for ReplayFile entities (replay records).</summary>
        public IReplayFileRepository Replays =>
            _replays ??= new ReplayRepository(_connectionString);

        /// <summary>Repository for BuildOrder entities (build order data).</summary>
        public IBuildOrderRepository BuildOrders =>
            _buildOrders ??= new BuildOrderRepository(_connectionString);

        /// <summary>Repository for UserAccount entities (user accounts).</summary>
        public IUserAccountRepository UserAccounts =>
            _userAccounts ??= new UserAccountRepository(_connectionString);

        /// <summary>Save all pending changes to the database.</summary>
        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // In a true Unit of Work with change tracking, this would batch all changes.
            // With our current simple repository, changes are already saved immediately.
            // This method is here for API compatibility and future enhancements.
            _logger.Debug("SaveChangesAsync called - changes are auto-saved in this repository");
            return await Task.FromResult(0);
        }

        public void Dispose()
        {
            // No resources to dispose with current implementation
            GC.SuppressFinalize(this);
        }
    }
}
