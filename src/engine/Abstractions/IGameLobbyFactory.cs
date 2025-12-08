using BarcodeRevealTool.Engine;
using BarcodeRevealTool.game.lobbies;

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
        /// <param name="appSettings">The resolved application settings</param>
        /// <returns>An ISoloGameLobby if successful, null otherwise</returns>
        ISoloGameLobby? CreateLobby(byte[] lobbyData, AppSettings appSettings);
    }
}
