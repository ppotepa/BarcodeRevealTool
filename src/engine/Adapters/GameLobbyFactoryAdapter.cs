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
            try
            {
                var appSettings = new AppSettings();
                configuration.GetSection("barcodeReveal").Bind(appSettings);

                var lobby = _gameLobbyFactory.CreateLobby(lobbyData, appSettings);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactoryAdapter] Lobby created successfully: {lobby}");
                return lobby as ISoloGameLobby;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactoryAdapter] Exception in CreateLobby: {ex}");
                throw;
            }
        }
    }
}
