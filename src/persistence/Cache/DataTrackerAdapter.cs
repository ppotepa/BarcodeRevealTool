using BarcodeRevealTool.Engine.Abstractions;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Adapter that implements the Engine's IDataTracker interface
    /// using the persistence layer's DataTrackingIntegrationService.
    /// </summary>
    public class DataTrackerAdapter : IDataTracker
    {
        private readonly DataTrackingIntegrationService _service;

        public DataTrackerAdapter(DataTrackingIntegrationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public async Task RecordLobbyDetectedAsync(
            int runNumber,
            string lobbyFilePath,
            string? opponentTag = null,
            string? opponentToon = null,
            string? manualOpponentTag = null,
            string? manualOpponentNickname = null)
        {
            await _service.RecordLobbyDetectedAsync(
                runNumber,
                lobbyFilePath,
                opponentTag,
                opponentToon,
                manualOpponentTag,
                manualOpponentNickname);
        }

        public async Task RecordMatchFinishedAsync(int runNumber, string replayFilePath, string? opponentTag = null)
        {
            await _service.RecordMatchFinishedAsync(runNumber, replayFilePath, opponentTag);
        }
    }
}
