using Microsoft.Extensions.Configuration;

namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Abstraction for creating game lobbies from binary data
    /// </summary>
    public interface IGameLobbyFactory
    {
        /// <summary>
        /// Creates a game lobby from binary lobby file data
        /// </summary>
        /// <param name="lobbyData">The raw bytes from the lobby.SC2Replay file</param>
        /// <param name="configuration">The application configuration</param>
        /// <returns>An ISoloGameLobby if successful, null otherwise</returns>
        ISoloGameLobby? CreateLobby(byte[] lobbyData, IConfiguration configuration);
    }
}
