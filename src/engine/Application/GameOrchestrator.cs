using System;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Application.Lobbies;
using BarcodeRevealTool.Engine.Domain.Services;
using BarcodeRevealTool.Engine.Game.Lobbies;
using BarcodeRevealTool.Engine.Presentation;

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
            IErrorRenderer errorRenderer)
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
                    _stateRenderer.RenderAwaitingState();
                }
            };

            await _stateMonitor.RunAsync(cancellationToken);
        }

        private async Task HandleInGameAsync(CancellationToken cancellationToken)
        {
            await _replaySyncService.SyncAsync(cancellationToken);
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

            var matches = _matchHistoryService.GetHistory(you.Tag, opponent.Tag, 5);
            var stats = _matchHistoryService.Analyze(matches);
            _historyRenderer.RenderMatchHistory(matches, stats);

            var steps = _buildOrderService.GetRecentBuild(opponent.Tag, 20);
            _buildOrderRenderer.RenderBuildOrder(steps);
            var pattern = _buildOrderService.AnalyzePattern(opponent.Tag, steps);
            _buildOrderRenderer.RenderBuildPattern(pattern);

            var profile = _profileService.BuildProfile(you.Tag, opponent.Tag);
            _historyRenderer.RenderOpponentProfile(profile);
        }
    }
}
