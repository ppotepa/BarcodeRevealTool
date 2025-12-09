using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies
{
    public class GameLobby : ISoloGameLobby
    {
        public GameLobby(Team team1, Team team2, string opponentTag)
        {
            Team1 = team1;
            Team2 = team2;
            OpponentTag = opponentTag;
        }

        public Team Team1 { get; }
        public Team Team2 { get; }
        public string OpponentTag { get; }
    }
}
