using BarcodeRevealTool.Engine.Abstractions;
using Microsoft.Extensions.Configuration;
using Sc2Pulse;

namespace BarcodeRevealTool.Engine
{
    public enum ToolState
    {
        Awaiting,
        InGame
    }

    /// <summary>
    /// Core engine for game monitoring and replay management.
    /// UI-agnostic - uses IOutputProvider for all output.
    /// </summary>
    public class GameEngine
    {
        private ISoloGameLobby? _cachedLobby;
        private bool _cacheInitialized = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private ToolState _currentState = ToolState.Awaiting;
        private Task? _monitoringTask;
        private readonly IOutputProvider _outputProvider;
        private readonly IReplayService _replayService;
        private readonly IGameLobbyFactory _gameLobbyFactory;
        private readonly IConfiguration _configuration;

        public GameEngine(IConfiguration configuration, IServiceProvider services, Sc2PulseClient pulseClient, IOutputProvider outputProvider, IReplayService replayService, IGameLobbyFactory gameLobbyFactory)
        {
            _configuration = configuration;
            configuration.GetSection("barcodeReveal").Bind(Configuration);
            Services = services;
            PulseClient = pulseClient;
            _outputProvider = outputProvider;
            _replayService = replayService;
            _gameLobbyFactory = gameLobbyFactory;
        }

        // Event for state changes
        public event EventHandler<ToolStateChangedEventArgs>? StateChanged;

        public string AppDataLocal
            => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public AppSettings? Configuration { get; private set; } = new();
        public ToolState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    ToolState previousState = _currentState;
                    _currentState = value;
                    OnStateChanged(previousState, value);
                }
            }
        }

        public string LobbyFilePath
            => Path.Combine(AppDataLocal, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");

        public Sc2PulseClient PulseClient { get; }
        public IServiceProvider Services { get; }

        public async Task Run()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Start cache initialization in background (don't block on it)
            // This allows the app to become responsive immediately
            var cacheInitTask = Task.Run(async () =>
            {
                if (!_cacheInitialized)
                {
                    await InitializeCacheAsync();
                    _cacheInitialized = true;
                }
            }, token);

            // Start background monitoring thread immediately
            _monitoringTask = MonitorGameStateAsync(token);

            // Display initial state
            DisplayCurrentState();

            // Wait for cancellation
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
        }

        public void Stop()
        {
            _cancellationTokenSource?.Cancel();
        }

        protected virtual void OnStateChanged(ToolState previousState, ToolState newState)
        {
            _outputProvider.RenderStateChange(previousState.ToString(), newState.ToString());
            StateChanged?.Invoke(this, new ToolStateChangedEventArgs(previousState, newState));
        }

        private void DisplayCurrentState()
        {
            _outputProvider.Clear();

            if (CurrentState == ToolState.Awaiting)
            {
                _outputProvider.RenderAwaitingState();
            }
            else if (CurrentState == ToolState.InGame && _cachedLobby is not null)
            {
                _outputProvider.RenderLobbyInfo(_cachedLobby, _cachedLobby.AdditionalData, _cachedLobby.LastBuildOrderEntry);
            }
        }

        private async Task InitializeCacheAsync()
        {
            try
            {
                await _replayService.InitializeCacheAsync();
                _cacheInitialized = true;
            }
            catch (Exception ex)
            {
                _outputProvider.RenderError($"Error during cache initialization: {ex.Message}");
                _cacheInitialized = true;
            }
        }

        private async Task SyncReplaysFromDiskAsync()
        {
            try
            {
                await _replayService.SyncReplaysFromDiskAsync();
            }
            catch (Exception ex)
            {
                _outputProvider.RenderError($"Error syncing replays from disk: {ex.Message}");
            }
        }

        private async Task MonitorGameStateAsync(CancellationToken cancellationToken)
        {
            int stateCheckIntervalMs = Configuration?.RefreshInterval ?? 500; // Check state frequently but lightly
            DateTime lastRefreshTime = DateTime.UtcNow;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool lobbyFileExists = File.Exists(LobbyFilePath);
                    ToolState newState = lobbyFileExists ? ToolState.InGame : ToolState.Awaiting;

                    // State change detected
                    if (newState != CurrentState)
                    {
                        CurrentState = newState;
                        lastRefreshTime = DateTime.UtcNow;

                        if (CurrentState == ToolState.InGame)
                        {
                            // Sync any new replays before processing the current lobby
                            await SyncReplaysFromDiskAsync();
                            await ProcessLobbyAsync();
                        }
                        else
                        {
                            // When leaving game, sync any replays that were recorded
                            await SyncReplaysFromDiskAsync();
                            DisplayCurrentState();
                        }
                    }

                    lastRefreshTime = DateTime.UtcNow;
                    await Task.Delay(stateCheckIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _outputProvider.RenderError($"Error in monitoring loop: {ex.Message}");
                    await Task.Delay(stateCheckIntervalMs, cancellationToken);
                }
            }
        }

        private async Task ProcessLobbyAsync()
        {
            try
            {
                if (!File.Exists(LobbyFilePath))
                {
                    _cachedLobby = null;
                    return;
                }

                var lobbyBytes = File.ReadAllBytes(LobbyFilePath);
                var lobby = _gameLobbyFactory.CreateLobby(lobbyBytes, _configuration);

                if (lobby is not null)
                {
                    _cachedLobby = lobby as ISoloGameLobby;

                    // Load opponent stats asynchronously in background
                    if (_cachedLobby is not null)
                    {
                        _ = _cachedLobby.EnsureAdditionalDataLoadedAsync();
                    }

                    DisplayCurrentState();
                }
                else
                {
                    _outputProvider.RenderError("Failed to parse lobby data.");
                    _cachedLobby = null;
                }
            }
            catch (Exception ex)
            {
                _outputProvider.RenderError($"Error processing lobby: {ex.Message}");
                _cachedLobby = null;
            }
        }
    }

    public class ToolStateChangedEventArgs : EventArgs
    {
        public ToolStateChangedEventArgs(ToolState previousState, ToolState newState)
        {
            PreviousState = previousState;
            NewState = newState;
        }

        public ToolState NewState { get; }
        public ToolState PreviousState { get; }
    }
}
