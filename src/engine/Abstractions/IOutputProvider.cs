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
        
        /// <summary>
        /// Handle periodic state updates (fired every 1500ms).
        /// Can be used for animations, refreshing display, etc.
        /// </summary>
        void HandlePeriodicStateUpdate(string state, ISoloGameLobby? lobby);
    }

    /// <summary>
    /// Game lobby interface - abstracted from concrete UI types
    /// </summary>
    public interface ISoloGameLobby
    {
        object? Team1 { get; }
        object? Team2 { get; }
        object? AdditionalData { get; }
        object? LastBuildOrderEntry { get; set; }
        Task EnsureAdditionalDataLoadedAsync();
    }
}
