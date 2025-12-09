using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game.Lobbies;

namespace BarcodeRevealTool.Engine.Abstractions
{
    public interface IGameLobbyFactory
    {
        ISoloGameLobby? CreateLobby(byte[] lobbyBytes, AppSettings settings);
    }
}
