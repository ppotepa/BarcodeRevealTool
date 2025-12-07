using BarcodeRevealTool.Game;

namespace BarcodeRevealTool.game.lobbies
{
    public interface ISoloGameLobby : IGameLobby
    {
        Team? Team2 { get; }
    }

    public interface IGameLobby
    {
        Team? Team1 { get; }

        void PrintLobbyInfo(TextWriter writer);
    }
}