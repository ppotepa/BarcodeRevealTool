using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.game.lobbies;
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
        /// <summary>
        /// State check interval - hard-coded to 1500ms (non-configurable).
        /// This is the frequency at which game state is checked and periodic events are fired.
        /// </summary>
        private const int StateCheckIntervalMs = 1500;

        private ISoloGameLobby? _cachedLobby;
        private bool _cacheInitialized = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private ToolState _currentState = ToolState.Awaiting;
        private Task? _monitoringTask;
        private readonly IOutputProvider _outputProvider;
        private readonly IReplayService _replayService;
        private readonly IGameLobbyFactory _gameLobbyFactory;
        private readonly IConfiguration _configuration;
        private readonly GameStateManager _gameStateManager = new();

        public GameEngine(IConfiguration configuration, IServiceProvider services, Sc2PulseClient pulseClient, IOutputProvider outputProvider, IReplayService replayService, IGameLobbyFactory gameLobbyFactory)
        {
            _configuration = configuration;
            configuration.GetSection("barcodeReveal").Bind(Configuration);
            Services = services;
            PulseClient = pulseClient;
            _outputProvider = outputProvider;
            _replayService = replayService;
            _gameLobbyFactory = gameLobbyFactory;

            // Subscribe to game state changes
            _gameStateManager.GameProcessStateChanged += OnGameProcessStateChanged;

            // Subscribe to cache operation completion to refresh display
            _replayService.OnCacheOperationComplete += () =>
            {
                System.Diagnostics.Debug.WriteLine($"[GameEngine] Cache operation complete, refreshing display");
                DisplayCurrentState();
            };
        }

        // Events for state management
        public event EventHandler<ToolStateChangedEventArgs>? StateChanged;
        public event EventHandler<PeriodicStateEventArgs>? PeriodicStateUpdate;

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

            // Start SC2 process monitor in background
            var sc2MonitorTask = MonitorSc2ProcessAsync(token);

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

        protected virtual void OnPeriodicStateUpdate(PeriodicStateEventArgs args)
        {
            PeriodicStateUpdate?.Invoke(this, args);
        }

        private void DisplayCurrentState()
        {
            _outputProvider.Clear();

            bool sc2IsRunning = _gameStateManager.IsStarCraft2Running();

            if (!sc2IsRunning)
            {
                _outputProvider.RenderAwaitingState();
                _outputProvider.RenderWarning("SC2 process not found. Please start StarCraft II.");
            }
            else if (CurrentState == ToolState.Awaiting)
            {
                _outputProvider.RenderWarning("SC2 process detected. Scanning for lobby...");
            }
            else if (CurrentState == ToolState.InGame)
            {
                if (_cachedLobby is not null)
                {
                    _outputProvider.RenderLobbyInfo(_cachedLobby, _cachedLobby.AdditionalData, _cachedLobby.LastBuildOrderEntry);
                }
                else
                {
                    _outputProvider.RenderWarning("Match in progress. Loading lobby data...");
                }
            }
        }

        private bool IsStarCraft2Running()
        {
            return _gameStateManager.IsStarCraft2Running();
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
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    // Only check for lobby if SC2 process is running
                    bool sc2IsRunning = _gameStateManager.IsStarCraft2Running();
                    bool lobbyFileExists = sc2IsRunning && File.Exists(LobbyFilePath);

                    // Update game process state (fires events if state changed)
                    _gameStateManager.UpdateGameProcessState(lobbyFileExists);

                    ToolState newState = lobbyFileExists ? ToolState.InGame : ToolState.Awaiting;
                    System.Diagnostics.Debug.WriteLine($"[GameEngine] State check: {newState}, SC2Running: {sc2IsRunning}, LobbyFileExists: {lobbyFileExists}");

                    // State change detected
                    if (newState != CurrentState)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameEngine] State transition: {CurrentState} -> {newState}");
                        CurrentState = newState;

                        if (CurrentState == ToolState.InGame)
                        {
                            System.Diagnostics.Debug.WriteLine($"[GameEngine] Match detected, syncing replays and processing lobby");
                            // Entering match: sync any new replays (to get opponent history)
                            await SyncReplaysFromDiskAsync();
                            await ProcessLobbyAsync();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[GameEngine] Match exited, saving replay");
                            // Exiting match: save the replay that just finished
                            await OnExitingGameAsync();
                            DisplayCurrentState();
                        }
                    }

                    // Fire periodic update event (every 1500ms, regardless of state change)
                    OnPeriodicStateUpdate(new PeriodicStateEventArgs
                    {
                        CurrentState = CurrentState,
                        CurrentLobby = CurrentState == ToolState.InGame ? _cachedLobby : null,
                        Timestamp = DateTime.UtcNow
                    });

                    await Task.Delay(StateCheckIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _outputProvider.RenderError($"Error in monitoring loop: {ex.Message}");
                    await Task.Delay(StateCheckIntervalMs, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Monitors whether SC2 process is running (32-bit or 64-bit) and logs status.
        /// Runs independently from main game state monitoring.
        /// </summary>
        private async Task MonitorSc2ProcessAsync(CancellationToken cancellationToken)
        {
            bool wasRunningLastCheck = false;

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    bool isRunning = _gameStateManager.IsStarCraft2Running();

                    // Only log when state changes
                    if (isRunning != wasRunningLastCheck)
                    {
                        if (isRunning)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SC2Monitor] StarCraft II process detected (SC2.exe or SC2_x64.exe)");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[SC2Monitor] StarCraft II process terminated");
                        }
                        wasRunningLastCheck = isRunning;

                        // Refresh display when process state changes
                        DisplayCurrentState();
                    }

                    // Check every 2 seconds
                    await Task.Delay(2000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SC2Monitor] Error: {ex.Message}");
                    await Task.Delay(2000, cancellationToken);
                }
            }
        }

        private async Task ProcessLobbyAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[GameEngine] ProcessLobbyAsync started");

            try
            {
                if (!File.Exists(LobbyFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[GameEngine] Lobby file not found at {LobbyFilePath}");
                    _cachedLobby = null;
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[GameEngine] Reading lobby file from {LobbyFilePath}");
                var lobbyBytes = File.ReadAllBytes(LobbyFilePath);
                System.Diagnostics.Debug.WriteLine($"[GameEngine] Lobby file size: {lobbyBytes.Length} bytes");
                var lobby = _gameLobbyFactory.CreateLobby(lobbyBytes, _configuration);

                if (lobby is not null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameEngine] Lobby parsed successfully");
                    _cachedLobby = lobby as ISoloGameLobby;

                    // Load opponent stats asynchronously in background
                    if (_cachedLobby is not null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameEngine] Starting background load of additional data");
                        _ = _cachedLobby.EnsureAdditionalDataLoadedAsync();
                    }

                    DisplayCurrentState();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GameEngine] Lobby creation returned null");
                    _outputProvider.RenderError("Failed to parse lobby data.");
                    _cachedLobby = null;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameEngine] Exception in ProcessLobbyAsync: {ex}");
                _outputProvider.RenderError($"Error processing lobby: {ex.Message}");
                _cachedLobby = null;
            }
        }

        /// <summary>
        /// Called when exiting a game (InGame → Awaiting state change).
        /// Saves the just-played replay to the database.
        /// </summary>
        private async Task OnExitingGameAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[GameEngine] OnExitingGameAsync started");
            try
            {
                // Find the most recently modified replay file (the one that just finished)
                var replayFolder = Path.Combine(
                    AppDataLocal,
                    "Temp",
                    "StarCraft II"
                );

                System.Diagnostics.Debug.WriteLine($"[GameEngine] Looking for replays in {replayFolder}");
                if (!Directory.Exists(replayFolder))
                {
                    System.Diagnostics.Debug.WriteLine($"[GameEngine] Replay folder does not exist");
                    return;
                }

                // Look for the latest replay file in the Replays folder
                var replaysDir = Path.Combine(replayFolder, "LastReplay");
                if (Directory.Exists(replaysDir))
                {
                    var replayFiles = Directory.GetFiles(replaysDir, "*.SC2Replay")
                        .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                        .FirstOrDefault();

                    if (replayFiles != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameEngine] Found replay file: {replayFiles}");
                        // Save only this one replay to the database
                        await _replayService.SaveReplayToDbAsync(replayFiles);
                        System.Diagnostics.Debug.WriteLine($"[GameEngine] Replay saved to database");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameEngine] No replay files found in {replaysDir}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GameEngine] LastReplay directory does not exist at {replaysDir}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameEngine] Exception in OnExitingGameAsync: {ex}");
                _outputProvider.RenderError($"Error saving replay to database: {ex.Message}");
            }
            finally
            {
                _cachedLobby = null;
            }
        }

        private void OnGameProcessStateChanged(object? sender, GameProcessStateChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[GameEngine] Game process state changed: {e.OldState} -> {e.NewState}");

            if (e.NewState == GameStateManager.GameProcessState.NotRunning)
            {
                // Game exited
                System.Diagnostics.Debug.WriteLine($"[GameEngine] StarCraft II has exited");
                CurrentState = ToolState.Awaiting;
                _cachedLobby = null;
            }
            else if (e.NewState == GameStateManager.GameProcessState.Running)
            {
                // Game started but no match yet
                System.Diagnostics.Debug.WriteLine($"[GameEngine] StarCraft II is running, waiting for match");
                CurrentState = ToolState.Awaiting;
                _cachedLobby = null;
            }
            // InMatch state is handled by the main state check logic
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

    /// <summary>
    /// Event args fired every 1500ms to update UI with current state (periodic refresh).
    /// Different from StateChanged - fires regularly regardless of state transitions.
    /// </summary>
    public class PeriodicStateEventArgs : EventArgs
    {
        public ToolState CurrentState { get; set; }
        public ISoloGameLobby? CurrentLobby { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

