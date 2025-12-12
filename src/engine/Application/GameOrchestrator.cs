using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Application.Lobbies;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Services;
using BarcodeRevealTool.Engine.Presentation;
using System.Linq;

namespace BarcodeRevealTool.Engine.Application
{
    public class GameOrchestrator
    {
        private readonly IGameStateMonitor _stateMonitor;
        private readonly IReplaySyncService _replaySyncService;
        private readonly ILobbyProcessor _lobbyProcessor;
        private readonly IMatchHistoryService _matchHistoryService;
        private readonly IBuildOrderService _buildOrderService;
        private readonly IOpponentProfileService _profileService;
        private readonly IGameStateRenderer _stateRenderer;
        private readonly IMatchHistoryRenderer _historyRenderer;
        private readonly IBuildOrderRenderer _buildOrderRenderer;
        private readonly IErrorRenderer _errorRenderer;
        private readonly IReplayPersistence _replayPersistence;
        private readonly IDataTracker _dataTracker;
        private readonly AppSettings _settings;
        private MatchContext? _activeMatchContext;

        public GameOrchestrator(
            IGameStateMonitor stateMonitor,
            IReplaySyncService replaySyncService,
            ILobbyProcessor lobbyProcessor,
            IMatchHistoryService matchHistoryService,
            IBuildOrderService buildOrderService,
            IOpponentProfileService profileService,
            IGameStateRenderer stateRenderer,
            IMatchHistoryRenderer historyRenderer,
            IBuildOrderRenderer buildOrderRenderer,
            IErrorRenderer errorRenderer,
            IReplayPersistence replayPersistence,
            IDataTracker dataTracker,
            AppSettings settings)
        {
            _stateMonitor = stateMonitor;
            _replaySyncService = replaySyncService;
            _lobbyProcessor = lobbyProcessor;
            _matchHistoryService = matchHistoryService;
            _buildOrderService = buildOrderService;
            _profileService = profileService;
            _stateRenderer = stateRenderer;
            _historyRenderer = historyRenderer;
            _buildOrderRenderer = buildOrderRenderer;
            _errorRenderer = errorRenderer;
            _replayPersistence = replayPersistence ?? throw new ArgumentNullException(nameof(replayPersistence));
            _dataTracker = dataTracker ?? throw new ArgumentNullException(nameof(dataTracker));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            await _replaySyncService.InitializeAsync(cancellationToken);
            _stateRenderer.RenderAwaitingState();

            _stateMonitor.StateChanged += async (_, args) =>
            {
                if (args.Current == ToolState.InGame)
                {
                    await HandleInGameAsync(cancellationToken);
                }
                else
                {
                    // Only sync when game finishes (InGame -> Awaiting)
                    // This ensures we pick up replays of games that just completed
                    if (args.Previous == ToolState.InGame && args.Current == ToolState.Awaiting)
                    {
                        await _replaySyncService.SyncAsync(cancellationToken);
                    }

                    _stateRenderer.RenderAwaitingState();
                }
            };

            await _stateMonitor.RunAsync(cancellationToken);
        }

        private async Task HandleInGameAsync(CancellationToken cancellationToken)
        {
            _activeMatchContext = null;

            // Do NOT sync here - game hasn't finished yet
            // Only sync after game is complete (InGame -> Awaiting)
            var lobby = await _lobbyProcessor.TryReadLobbyAsync(cancellationToken);

            if (lobby is null)
            {
                _errorRenderer.RenderWarning("Lobby file not available.");
                return;
            }

            // Store all files encountered in StarCraft II directories during this lobby detection
            // This captures all relevant files for analysis, including map caches, replay data, etc.
            await StoreAllEncounteredFilesAsync(cancellationToken);

            // Get manual opponent info if in debug mode
            var manualOpponentTag = _settings.Debug?.ManualBattleTag;
            var manualOpponentNickname = _settings.Debug?.ManualNickname;

            // Record that a lobby was detected (with the opponent info)
            await _dataTracker.RecordLobbyDetectedAsync(
                runNumber: 0,
                lobbyFilePath: "", // We're now storing all files instead
                opponentTag: lobby.OpponentTag,
                opponentToon: null,
                manualOpponentTag: manualOpponentTag,
                manualOpponentNickname: manualOpponentNickname);

            _stateRenderer.RenderInGameState(lobby);

            if (!LobbyInsights.TryResolvePlayers(lobby, out var you, out var opponent))
            {
                _errorRenderer.RenderWarning("Unable to extract opponent from lobby.");
                return;
            }

            var opponentToon = _matchHistoryService.GetLastKnownOpponentToon(opponent.Tag);
            if (!string.IsNullOrWhiteSpace(opponentToon))
            {
                opponent.Toon = opponentToon;
            }

            _activeMatchContext = new MatchContext(you.Tag, opponent.Tag, opponent.Toon);

            var matchLimit = Math.Max(1, _settings.Replays?.MatchHistoryLimit ?? 5);

            var matches = _matchHistoryService.GetHistory(you.Tag, opponent.Tag, matchLimit, opponentToon);
            var stats = _matchHistoryService.Analyze(matches);
            _historyRenderer.RenderMatchHistory(matches, stats);

            var steps = _buildOrderService.GetRecentBuild(opponent.Tag, 20);
            _buildOrderRenderer.RenderBuildOrder(steps);
            var pattern = _buildOrderService.AnalyzePattern(opponent.Tag, steps);
            _buildOrderRenderer.RenderBuildPattern(pattern);

            var profile = await _profileService.BuildProfileAsync(you.Tag, opponent.Tag, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(profile.OpponentToon))
            {
                opponent.Toon = profile.OpponentToon;
                _activeMatchContext = _activeMatchContext?.WithToon(profile.OpponentToon);
            }
            _historyRenderer.RenderOpponentProfile(profile);
        }

        private async Task StoreAllEncounteredFilesAsync(CancellationToken cancellationToken)
        {
            // Look for the actual battle lobby file in the TempWriteReplayP1 directory
            var searchDirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "Temp", "Starcraft II", "TempWriteReplayP1"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                    "Temp", "StarCraft II", "TempWriteReplayP1"),
            };

            foreach (var dir in searchDirs)
            {
                if (!Directory.Exists(dir))
                    continue;

                try
                {
                    // Look specifically for the battle lobby file
                    var lobbyFilePath = Path.Combine(dir, "replay.server.battlelobby");
                    if (File.Exists(lobbyFilePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(lobbyFilePath);
                            
                            // Skip if file is incomplete (less than 512 bytes)
                            if (fileInfo.Length < 512)
                            {
                                System.Diagnostics.Debug.WriteLine($"Skipping incomplete lobby file: {fileInfo.Length} bytes");
                                continue;
                            }

                            // Store this lobby file
                            await _dataTracker.RecordLobbyDetectedAsync(
                                runNumber: 0,
                                lobbyFilePath: lobbyFilePath,
                                opponentTag: null,
                                opponentToon: null);
                            
                            System.Diagnostics.Debug.WriteLine($"Stored lobby file: {lobbyFilePath} ({fileInfo.Length} bytes)");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to store lobby file {lobbyFilePath}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to scan directory {dir}: {ex.Message}");
                }
            }
        }

        private static string GetLobbyFilePath()
        {
            // Try multiple possible locations for the lobby file
            // Different SC2 versions and installations may use different paths
            
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var commonPaths = new[]
            {
                // Standard location in LocalAppData
                Path.Combine(appData, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby"),
                
                // Alternative location with different casing
                Path.Combine(appData, "Temp", "StarCraft II", "TempWriteReplayP1", "replay.server.battlelobby"),
                
                // ProgramData location (some installations)
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), 
                    "Blizzard Entertainment", "Battle.net", "TempWriteReplayP1", "replay.server.battlelobby"),
            };

            // Return the first path that exists, otherwise return the default
            foreach (var path in commonPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }

            // Default to the standard location if none found
            return commonPaths[0];
        }

        private sealed record MatchContext(string YouTag, string OpponentTag, string? OpponentToon)
        {
            public MatchContext WithToon(string toon) => new MatchContext(YouTag, OpponentTag, toon);
        }
    }
}
