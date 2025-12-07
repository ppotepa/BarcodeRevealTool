using BarcodeRevealTool.game.lobbies;
using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Game
{

    /// <summary>
    /// as of now we wanna just support 1v1 mode, prolly the structure is a bit different for team games, also arcase, 
    /// would need dig deeper into the lobby file structure
    /// </summary>
    internal class GameLobbyFactory
    {
        private readonly byte[]? bytes;
        private readonly MatchCollection playersCollectionMatch;



        public IGameLobby CreateLobby(byte[] bytes)
        {
            var lobby = new GameLobby();

            var playersCollectionMatch = Pattern.Matches(
                new string([.. bytes.Select(x => (char)x)])
            );

            if (playersCollectionMatch.Count % 2 == 0)
            {
                if (playersCollectionMatch.Count / 3 is 2)
                {
                    return new GameLobby()
                    {
                        Team1 = new("Team 1")
                        {
                            Players = [
                                new Player {
                                    NickName = playersCollectionMatch[0].Groups["name"].Value,
                                    Tag = playersCollectionMatch[2].Groups["name"].Value
                                },
                            ]
                        }!,
                        Team2 = new("Team 2")
                        {
                            Players = [
                                new Player {
                                    NickName = playersCollectionMatch[3].Groups["name"].Value,
                                    Tag = playersCollectionMatch[5].Groups["name"].Value
                                },
                            ]
                        },
                        OppositeTeam = (l) => new[]
                         {
                            l.Team1, l.Team2!
                        }
                        .Where(team => !team.Players.Any(p => p.Tag == "Originator#21343")).First()!,
                        UsersTeam = (l) => new[]
                        {
                            l.Team1, l.Team2!
                        }.Where(team => team.Players.Any(p => p.Tag == "Originator#21343")).First()!
                    };
                }
            }

            throw new InvalidOperationException("Unsupported lobby format or player count.");
        }




        public Regex Pattern = new Regex("(?<name>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");

    }
}
