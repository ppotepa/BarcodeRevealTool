using BarcodeRevealTool.Engine.Game;
using BarcodeRevealTool.Engine.Game.Lobbies;

namespace BarcodeRevealTool.Engine.Application.Lobbies
{
    internal static class LobbyInsights
    {
        public static bool TryResolvePlayers(
            ISoloGameLobby lobby,
            out Player yourPlayer,
            out Player opponent)
        {
            yourPlayer = lobby.Team1.Players.FirstOrDefault() ?? new Player();
            opponent = lobby.Team2.Players.FirstOrDefault() ?? new Player();
            return !string.IsNullOrEmpty(opponent.Tag);
        }
    }
}
