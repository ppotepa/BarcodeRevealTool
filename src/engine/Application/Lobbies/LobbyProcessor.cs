using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game.Lobbies;

namespace BarcodeRevealTool.Engine.Application.Lobbies
{
    public class LobbyProcessor : ILobbyProcessor
    {
        private readonly IGameLobbyFactory _factory;
        private readonly AppSettings _settings;

        public LobbyProcessor(IGameLobbyFactory factory, AppSettings settings)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<ISoloGameLobby?> TryReadLobbyAsync(CancellationToken cancellationToken)
        {
            var lobbyPath = GetLobbyPath();
            if (!File.Exists(lobbyPath))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(lobbyPath, cancellationToken);
            return _factory.CreateLobby(bytes, _settings);
        }

        private static string GetLobbyPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");
        }
    }
}
