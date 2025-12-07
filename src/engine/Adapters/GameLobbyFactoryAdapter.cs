using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Game;
using Microsoft.Extensions.Configuration;

namespace BarcodeRevealTool.Adapters
{
    /// <summary>
    /// Adapter that implements IGameLobbyFactory using the tool's GameLobbyFactory
    /// </summary>
    public class GameLobbyFactoryAdapter : IGameLobbyFactory
    {
        private readonly GameLobbyFactory _gameLobbyFactory;

        public GameLobbyFactoryAdapter()
        {
            _gameLobbyFactory = new GameLobbyFactory();
        }

        public ISoloGameLobby? CreateLobby(byte[] lobbyData, IConfiguration configuration)
        {
            var appSettings = new AppSettings();
            configuration.GetSection("barcodeReveal").Bind(appSettings);

            var lobby = _gameLobbyFactory.CreateLobby(lobbyData, appSettings);
            return lobby as ISoloGameLobby;
        }
    }
}
