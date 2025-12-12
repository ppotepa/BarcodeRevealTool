namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Abstraction for tracking game data like lobbies and matches.
    /// Implemented by the persistence layer.
    /// </summary>
    public interface IDataTracker
    {
        /// <summary>
        /// Record that a lobby was detected and process it for storage.
        /// </summary>
        Task RecordLobbyDetectedAsync(
            int runNumber,
            string lobbyFilePath,
            string? opponentTag = null,
            string? opponentToon = null,
            string? manualOpponentTag = null,
            string? manualOpponentNickname = null);

        /// <summary>
        /// Record that a match finished and a replay was saved.
        /// </summary>
        Task RecordMatchFinishedAsync(int runNumber, string replayFilePath, string? opponentTag = null);
    }
}
