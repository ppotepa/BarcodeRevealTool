using Serilog;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for cleaning up incomplete DebugSession records on application startup.
    /// Ensures that any "InProgress" sessions from previous runs are marked as completed.
    /// </summary>
    public class DebugSessionCleanupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger _logger;

        public DebugSessionCleanupService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _logger = Log.ForContext<DebugSessionCleanupService>();
        }

        /// <summary>
        /// Closes any InProgress debug sessions from previous application runs.
        /// This ensures clean state for new debug session on startup.
        /// </summary>
        public async Task CleanupAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get all InProgress debug sessions
                var inProgressSessions = await _unitOfWork.DebugSessions
                    .GetAllAsync(s => s.Status == "InProgress", cancellationToken);

                if (!inProgressSessions.Any())
                {
                    _logger.Debug("No InProgress DebugSession records to cleanup");
                    return;
                }

                _logger.Information("Found {Count} InProgress DebugSession records, marking as completed", inProgressSessions.Count);

                // Mark each as completed
                foreach (var session in inProgressSessions)
                {
                    session.UpdatedAt = DateTime.UtcNow;
                    session.Status = "Completed";
                    await _unitOfWork.DebugSessions.UpdateAsync(session, cancellationToken);
                    _logger.Debug("Completed DebugSession {SessionId} from run {RunNumber}", session.Id, session.RunNumber);
                }

                _logger.Information("Successfully cleaned up {Count} DebugSession records", inProgressSessions.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to cleanup InProgress DebugSession records");
                // Don't throw - allow application to continue even if cleanup fails
            }
        }
    }
}
