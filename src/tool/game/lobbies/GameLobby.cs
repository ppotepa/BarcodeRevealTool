using BarcodeRevealTool.game.lobbies;

namespace BarcodeRevealTool.Game
{

    /// <summary>
    /// as of now we wanna just support 1v1 mode, prolly the structure is a bit different for team games, also arcase, 
    /// would need dig deeper into the lobby file structure
    /// </summary>
    internal class GameLobby : ISoloGameLobby
    {
        public Team? Team1 { get; init; }

        public Team? Team2 { get; init; }

        public Func<ISoloGameLobby, Team> UsersTeam { get; init; }
        public Func<ISoloGameLobby, Team> OppositeTeam { get; init; }

        public void PrintAdditionalPlayerData()
        {
            //throw new NotImplementedException();
        }
    }
}
