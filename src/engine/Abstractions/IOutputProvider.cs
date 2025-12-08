using BarcodeRevealTool.game.lobbies;

namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Abstraction for console/UI output. Allows Engine to be UI-agnostic.
    /// </summary>
    public interface IOutputProvider
    {
        void Clear();
        void RenderAwaitingState();
        void RenderStateChange(string from, string to);
        void RenderCacheInitializingMessage();
        void RenderCacheSyncMessage();
        void RenderCacheProgress(int current, int total);
        void RenderCacheComplete();
        void RenderSyncComplete(int newReplays);
        void RenderWarning(string message);
        void RenderError(string message);
        void RenderLobbyInfo(ISoloGameLobby lobby, object? additionalData, object? lastBuildOrder);
        void RenderOpponentMatchHistory(List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName)> history);

        /// <summary>
        /// Handle periodic state updates (fired every 1500ms).
        /// Can be used for animations, refreshing display, etc.
        /// </summary>
        void HandlePeriodicStateUpdate(string state, ISoloGameLobby? lobby);
    }
}
