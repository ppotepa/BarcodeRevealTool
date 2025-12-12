using Serilog;
using BarcodeRevealTool.Persistence.Repositories;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;
using System.Text.Json;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Coordinates data tracking across all data tracking operations.
    /// Uses the Repository pattern (Unit of Work) to track all database operations from a single place.
    /// This service ensures that user actions and lobby files are properly recorded during a run.
    /// </summary>
    public class DataTrackingIntegrationService
    {
        private readonly LobbyFileService _lobbyFileService;
        private readonly ConfigInitializationService _configService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger _logger = Log.ForContext<DataTrackingIntegrationService>();
        private long? _currentDebugSessionId;

        public DataTrackingIntegrationService(
            LobbyFileService lobbyFileService,
            ConfigInitializationService configService,
            IUnitOfWork unitOfWork)
        {
            _lobbyFileService = lobbyFileService ?? throw new ArgumentNullException(nameof(lobbyFileService));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <summary>
        /// Initialize a new debug session for this run.
        /// Should be called in Program.cs after creating services.
        /// </summary>
        public void InitializeDebugSession(int runNumber, string? presetBattleTag = null)
        {
            try
            {
                var session = new DebugSessionEntity
                {
                    RunNumber = runNumber,
                    PresetUserBattleTag = presetBattleTag,
                    TotalMatchesPlayed = 0,
                    TotalLobbiesProcessed = 0,
                    Status = "InProgress",
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                var sessionId = _unitOfWork.DebugSessions.AddAsync(session).GetAwaiter().GetResult();
                _currentDebugSessionId = sessionId;

                _logger.Information("Initialized debug session {SessionId} for run {RunNumber}", sessionId, runNumber);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize debug session for run {RunNumber}", runNumber);
            }
        }

        /// <summary>
        /// Record that a lobby was detected and process it for storage.
        /// Call this when GameOrchestrator detects a new lobby.
        /// </summary>
        public async Task RecordLobbyDetectedAsync(
            int runNumber,
            string lobbyFilePath,
            string? opponentTag = null,
            string? opponentToon = null,
            string? manualOpponentTag = null,
            string? manualOpponentNickname = null)
        {
            try
            {
                if (_currentDebugSessionId == null)
                    return;

                var matchIndex = await GetNextMatchIndexAsync(runNumber);

                // Store the lobby file using existing service
                var lobbyFileId = await _lobbyFileService.StoreLobbyFileAsync(
                    runNumber: runNumber,
                    lobbyFilePath: lobbyFilePath,
                    matchIndex: matchIndex,
                    detectedOpponentTag: opponentTag,
                    detectedOpponentToon: opponentToon,
                    debugSessionId: _currentDebugSessionId
                );

                // If manual opponent info is provided, update the debug session with it
                if (!string.IsNullOrWhiteSpace(manualOpponentTag) || !string.IsNullOrWhiteSpace(manualOpponentNickname))
                {
                    var session = await _unitOfWork.DebugSessions.GetByIdAsync(_currentDebugSessionId.Value);
                    if (session != null)
                    {
                        if (!string.IsNullOrWhiteSpace(manualOpponentTag))
                            session.ManualOpponentBattleTag = manualOpponentTag;
                        if (!string.IsNullOrWhiteSpace(manualOpponentNickname))
                            session.ManualOpponentNickname = manualOpponentNickname;

                        session.UpdatedAt = DateTime.UtcNow;
                        await _unitOfWork.DebugSessions.UpdateAsync(session);
                    }
                }

                // Increment lobby counter
                await IncrementDebugSessionLobbiesAsync(_currentDebugSessionId.Value);

                _logger.Debug("Recorded lobby detection: {OpponentTag}, stored as file ID {LobbyFileId}", opponentTag, lobbyFileId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record lobby detection");
            }
        }

        /// <summary>
        /// Record that a match finished and a replay was saved.
        /// Call this after ReplaySyncService saves a replay to database.
        /// </summary>
        public async Task RecordMatchFinishedAsync(int runNumber, string replayFilePath, string? opponentTag = null)
        {
            try
            {
                if (_currentDebugSessionId == null)
                    return;

                // Increment match counter
                await IncrementDebugSessionMatchesAsync(_currentDebugSessionId.Value);

                _logger.Debug("Recorded match finished: {OpponentTag}", opponentTag);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record match finished");
            }
        }

        /// <summary>
        /// Mark the debug session as complete.
        /// Should be called in Program.cs finally block.
        /// </summary>
        public async Task CompleteDebugSessionAsync(int runNumber, int exitCode)
        {
            try
            {
                if (_currentDebugSessionId == null)
                    return;

                var session = await _unitOfWork.DebugSessions.GetByIdAsync(_currentDebugSessionId.Value);
                if (session != null)
                {
                    session.Status = exitCode == 0 ? "Completed" : "Failed";
                    session.ExitCode = exitCode;
                    session.UpdatedAt = DateTime.UtcNow;

                    await _unitOfWork.DebugSessions.UpdateAsync(session);
                    _logger.Information("Completed debug session {SessionId} with exit code {ExitCode}", _currentDebugSessionId, exitCode);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to complete debug session");
            }
        }

        private async Task IncrementDebugSessionMatchesAsync(long debugSessionId)
        {
            try
            {
                var session = await _unitOfWork.DebugSessions.GetByIdAsync(debugSessionId);
                if (session != null)
                {
                    session.TotalMatchesPlayed++;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.DebugSessions.UpdateAsync(session);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to increment match counter");
            }
        }

        private async Task IncrementDebugSessionLobbiesAsync(long debugSessionId)
        {
            try
            {
                var session = await _unitOfWork.DebugSessions.GetByIdAsync(debugSessionId);
                if (session != null)
                {
                    session.TotalLobbiesProcessed++;
                    session.UpdatedAt = DateTime.UtcNow;
                    await _unitOfWork.DebugSessions.UpdateAsync(session);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to increment lobby counter");
            }
        }

        private async Task<int> GetNextMatchIndexAsync(int runNumber)
        {
            try
            {
                var lobbyFiles = await _unitOfWork.LobbyFiles.GetAllAsync(lf => lf.RunNumber == runNumber);
                var maxIndex = lobbyFiles.Max(lf => (int?)lf.MatchIndex) ?? 0;
                return maxIndex + 1;
            }
            catch
            {
                return 0;
            }
        }
    }
}
