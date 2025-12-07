using BarcodeRevealTool.Game;

namespace BarcodeRevealTool.game.lobbies
{
    internal interface ISoloGameLobby : IGameLobby
    {
        Team? Team2 { get; }
    }

    internal interface IGameLobby
    {
        Team? Team1 { get; }

        void PrintAdditionalPlayerData();
        void PrintLobbyInfo(TextWriter writer)
            => writer.WriteLine(writer == null ? "No writer provided" : Team1?.ToString());
    }
}