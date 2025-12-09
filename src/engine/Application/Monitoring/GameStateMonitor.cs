using System;
using System.Threading;
using System.Threading.Tasks;

namespace BarcodeRevealTool.Engine.Application.Monitoring
{
    public class GameStateMonitor : Abstractions.IGameStateMonitor
    {
        private readonly GameStateManager _stateManager = new();
        private ToolState _currentState = ToolState.Awaiting;

        public ToolState CurrentState => _currentState;

        public event EventHandler<ToolStateChangedEventArgs>? StateChanged;

        public async Task RunAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sc2Running = _stateManager.IsStarCraft2Running();
                var lobbyDetected = sc2Running && LobbyFileExists();
                var newState = lobbyDetected ? ToolState.InGame : ToolState.Awaiting;

                if (newState != _currentState)
                {
                    var previous = _currentState;
                    _currentState = newState;
                    StateChanged?.Invoke(this, new ToolStateChangedEventArgs(previous, newState));
                }

                await Task.Delay(1500, cancellationToken);
            }
        }

        private static bool LobbyFileExists()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var path = Path.Combine(appData, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");
            return File.Exists(path);
        }
    }
}
