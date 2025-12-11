using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Application.Lobbies;
using BarcodeRevealTool.Engine.Config;
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

        private sealed record MatchContext(string YouTag, string OpponentTag, string? OpponentToon)
        {
            public MatchContext WithToon(string toon) => new MatchContext(YouTag, OpponentTag, toon);
        }
    }
}
