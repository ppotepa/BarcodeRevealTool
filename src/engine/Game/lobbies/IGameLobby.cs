using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies
{
    public interface IGameLobby
    {
        Team Team1 { get; }
        Team Team2 { get; }
    }

    public interface ISoloGameLobby : IGameLobby
    {
        string OpponentTag { get; }
    }
}
