using BarcodeRevealTool.game.lobbies;
using BarcodeRevealTool.Game;
using BarcodeRevealTool.Replay;
using Microsoft.Extensions.Configuration;
using Sc2Pulse;

namespace BarcodeRevealTool
{
    internal enum ToolState
    {
        Awaiting,
        InGame
    }

    internal class RevealTool
    {
        private IGameLobby? _cachedLobby;
        private bool _cacheInitialized = false;
        private CancellationTokenSource? _cancellationTokenSource;
        private ToolState _currentState = ToolState.Awaiting;
        private Task? _monitoringTask;

        public RevealTool(IConfiguration configuration, IServiceProvider services, Sc2PulseClient pulseClient)
        {
            configuration.Bind(Configuration);
            Services = services;
            PulseClient = pulseClient;
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
                    var previousState = _currentState;
                    _currentState = value;
                    OnStateChanged(previousState, value);
                }
            }
        }

        public string LobbyFilePath
            => Path.Combine(AppDataLocal, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");

        public Sc2PulseClient PulseClient { get; }
        public IServiceProvider Services { get; }
        private string CacheLockFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
        public async Task Run()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            // Initialize cache on startup if not already done
            if (!_cacheInitialized)
            {
                await InitializeCacheAsync();
            }

            // Start background monitoring thread
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
            Console.WriteLine($"[State Change] {previousState} → {newState}");
            StateChanged?.Invoke(this, new ToolStateChangedEventArgs(previousState, newState));
        }

        private void DisplayCurrentState()
        {
            Console.Clear();

            if (CurrentState == ToolState.Awaiting)
            {
                Console.WriteLine("Awaiting for the game to start...");
            }
            else if (CurrentState == ToolState.InGame && _cachedLobby is not null)
            {
                _cachedLobby.PrintLobbyInfo(Console.Out);
            }
        }


        private async Task InitializeCacheAsync()
        {
            try
            {
                // Check if cache has already been built
                if (File.Exists(CacheLockFile))
                {
                    Console.WriteLine("Cache lock file found. Syncing with disk...");
                    BuildOrderReader.InitializeCache();
                    await SyncReplaysFromDiskAsync();
                    _cacheInitialized = true;
                    return;
                }

                Console.WriteLine("First startup detected. Building replay cache...");

                // Initialize the cache/database
                BuildOrderReader.InitializeCache();

                // Scan replay folder and populate cache
                if (!string.IsNullOrEmpty(Configuration?.Replays?.Folder))
                {
                    var searchOption = (Configuration?.Replays?.Recursive ?? false)
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                    var replayFiles = Directory.GetFiles(
                        Configuration.Replays.Folder,
                        "*.SC2Replay",
                        searchOption);

                    Console.WriteLine($"Scanning {replayFiles.Length} replays for cache...");

                    // Scan all replays to populate the cache
                    for (int i = 0; i < replayFiles.Length; i++)
                    {
                        try
                        {
                            // This will cache metadata without requiring a player search
                            var metadata = BuildOrderReader.GetReplayMetadataFast(replayFiles[i]);
                            var database = BuildOrderReader.GetDatabase();

                            if (metadata != null && database != null)
                            {
                                database.CacheMetadata(metadata);
                            }

                            // Display progress on single line
                            Console.Write($"\rCaching in progress.... {i + 1} of {replayFiles.Length}");
                        }
                        catch (Exception ex)
                        {
                            // Continue on individual replay errors
                            Console.WriteLine($"\n  Warning: Failed to cache {Path.GetFileName(replayFiles[i])}: {ex.Message}");
                        }
                    }

                    Console.WriteLine("\nCache population complete.");
                }

                // Create cache lock file to prevent re-scanning on future startups
                File.WriteAllText(CacheLockFile, DateTime.UtcNow.ToString("O"));
                Console.WriteLine("Cache lock file created.");

                _cacheInitialized = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cache initialization: {ex.Message}");
                _cacheInitialized = true; // Mark as initialized anyway to prevent repeated errors
            }
        }

        /// <summary>
        /// Scan replay directory and add any new replays to the database.
        /// Called on startup (after initial cache) and when state changes to detect new replays.
        /// </summary>
        private async Task SyncReplaysFromDiskAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(Configuration?.Replays?.Folder))
                    return;

                var searchOption = (Configuration?.Replays?.Recursive ?? false)
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var replayFiles = Directory.GetFiles(
                    Configuration.Replays.Folder,
                    "*.SC2Replay",
                    searchOption);

                var database = BuildOrderReader.GetDatabase();
                if (database == null)
                    return;

                int newReplaysAdded = 0;

                // Check each replay file to see if it's in the database
                foreach (var replayFile in replayFiles)
                {
                    try
                    {
                        // Check if this replay is already in the database
                        var existing = database.GetReplayByFilePath(replayFile);
                        if (existing != null)
                            continue; // Already cached

                        // New replay - extract metadata and add it
                        var metadata = BuildOrderReader.GetReplayMetadataFast(replayFile);
                        if (metadata != null)
                        {
                            database.CacheMetadata(metadata);
                            newReplaysAdded++;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Failed to sync {Path.GetFileName(replayFile)}: {ex.Message}");
                    }
                }

                if (newReplaysAdded > 0)
                {
                    Console.WriteLine($"Synced {newReplaysAdded} new replays from disk.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing replays from disk: {ex.Message}");
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
                    //// If in game, check if we need to refresh based on interval
                    //else if (CurrentState == ToolState.InGame)
                    //{
                    //    int refreshInterval = Configuration?.RefreshInterval ?? 2000;
                    //    if ((DateTime.UtcNow - lastRefreshTime).TotalMilliseconds >= refreshInterval)
                    //    {
                    //        await ProcessLobbyAsync();

                    //    }
                    //}
                    lastRefreshTime = DateTime.UtcNow;
                    await Task.Delay(stateCheckIntervalMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in monitoring loop: {ex.Message}");
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
                var factory = Services.GetService(typeof(GameLobbyFactory)) as GameLobbyFactory;
                var lobby = factory?.CreateLobby(lobbyBytes, Configuration);

                if (lobby is not null)
                {
                    _cachedLobby = lobby;
                    DisplayCurrentState();
                }
                else
                {
                    Console.WriteLine("Failed to parse lobby data.");
                    _cachedLobby = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing lobby: {ex.Message}");
                _cachedLobby = null;
            }
        }
    }

    internal class ToolStateChangedEventArgs : EventArgs
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