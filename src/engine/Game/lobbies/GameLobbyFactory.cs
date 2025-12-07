using BarcodeRevealTool.game.lobbies;
using BarcodeRevealTool.Replay;
using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Game
{
    /// <summary>
    /// as of now we wanna just support 1v1 mode, prolly the structure is a bit different for team games, also arcase,
    /// would need dig deeper into the lobby file structure
    /// </summary>
    public class GameLobbyFactory
    {
        private static readonly Regex PlayerNamePattern = new("(?<name>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");

        private readonly Func<Team, string, bool> TeamContainsUserPredicate =
            (team, userBattleTag) => team!.Players.Any(p => p.Tag == userBattleTag);

        private Func<Team?, string, bool> TeamMatchesPredicate =>
            (team, userBattleTag) => TeamContainsUserPredicate(team!, userBattleTag);

        public IGameLobby CreateLobby(byte[] bytes, AppSettings? configuration)
        {
            var playerMatches = ExtractPlayerMatches(bytes);

            ValidateLobbyFormat(playerMatches);

            var team1 = CreateTeam("Team 1", playerMatches, team1Index: true);
            var team2 = CreateTeam("Team 2", playerMatches, team1Index: false);

            var userBattleTag = configuration?.User.BattleTag ?? string.Empty;
            var (usersTeamSelector, oppositeTeamSelector) = CreateTeamSelectors(userBattleTag);

            var lobby = new GameLobby()
            {
                Team1 = team1,
                Team2 = team2,
                OppositeTeam = oppositeTeamSelector,
                UsersTeam = usersTeamSelector
            };

            // Fetch last build order entry asynchronously
            var a = PopulateLastBuildOrderAsync(lobby, configuration);

            // AdditionalData will be lazily initialized in PrintLobbyInfo if needed
            return lobby;
        }

        private async Task PopulateLastBuildOrderAsync(GameLobby lobby, AppSettings? configuration)
        {
            try
            {
                if (configuration?.Replays.Folder == null || lobby.OppositeTeam == null)
                    return;

                // Get the first player of the opposite team
                var oppositeTeamPlayers = lobby.OppositeTeam(lobby).Players;
                if (oppositeTeamPlayers.Count == 0)
                    return;

                var oppositePlayer = oppositeTeamPlayers.First();

                var buildOrder = await BuildOrderReader.Read(
                    configuration.Replays.Folder,
                    oppositePlayer.Tag,
                    configuration.Replays.Recursive,
                    lobby.OppositeTeam
                );

                // Get the last entry from the build order
                var lastEntry = buildOrder.Entries
                    .OrderByDescending(e => e.TimeSeconds)
                    .FirstOrDefault();

                lobby.LastBuildOrderEntry = lastEntry;
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Failed to populate last build order: {ex.Message}");
            }
        }

        private MatchCollection ExtractPlayerMatches(byte[] bytes)
        {
            var byteString = new string([.. bytes.Select(@byte => (char)@byte)]);
            return PlayerNamePattern.Matches(byteString);
        }

        private void ValidateLobbyFormat(MatchCollection playerMatches)
        {
            bool isValidFormat = playerMatches.Count % 2 == 0 && playerMatches.Count / 3 == 2;

            if (!isValidFormat)
            {
                throw new InvalidOperationException("Unsupported lobby format or player count.");
            }
        }

        private Team CreateTeam(string teamName, MatchCollection playerMatches, bool team1Index)
        {
            int nameMatchIndex = team1Index ? 0 : 3;
            int tagMatchIndex = team1Index ? 2 : 5;

            var player = new Player
            {
                NickName = playerMatches[nameMatchIndex].Groups["name"].Value,
                Tag = playerMatches[tagMatchIndex].Groups["name"].Value
            };

            return new(teamName) { Players = [player] };
        }

        private (Func<ISoloGameLobby, Team>, Func<ISoloGameLobby, Team>) CreateTeamSelectors(string userBattleTag)
        {
            var usersTeamSelector = (ISoloGameLobby lobby) => GetTeamByPredicate(lobby, userBattleTag, match: true);
            var oppositeTeamSelector = (ISoloGameLobby lobby) => GetTeamByPredicate(lobby, userBattleTag, match: false);

            return (usersTeamSelector, oppositeTeamSelector);
        }

        private Team GetTeamByPredicate(ISoloGameLobby lobby, string userBattleTag, bool match)
        {
            var teams = new[] { lobby.Team1, lobby.Team2! };
            var predicate = match
                ? (Func<Team?, bool>)(team => TeamMatchesPredicate(team, userBattleTag))
                : (team => !TeamMatchesPredicate(team, userBattleTag));

            return teams.Where(predicate).First()!;
        }
    }
}
