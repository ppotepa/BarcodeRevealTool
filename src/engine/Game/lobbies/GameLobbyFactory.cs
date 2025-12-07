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
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] CreateLobby called with {bytes.Length} bytes");
            try
            {
                var playerMatches = ExtractPlayerMatches(bytes);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Extracted {playerMatches.Count} player matches");

                ValidateLobbyFormat(playerMatches);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Lobby format validation passed");

                var team1 = CreateTeam("Team 1", playerMatches, team1Index: true);
                var team2 = CreateTeam("Team 2", playerMatches, team1Index: false);

                var userBattleTag = configuration?.User.BattleTag ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] User battle tag: {userBattleTag}");
                var (usersTeamSelector, oppositeTeamSelector) = CreateTeamSelectors(userBattleTag);

                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Creating lobby with Team1: {team1?.Players.FirstOrDefault()?.Tag}, Team2: {team2?.Players.FirstOrDefault()?.Tag}");
                var lobby = new GameLobby()
                {
                    Team1 = team1,
                    Team2 = team2,
                    OppositeTeam = oppositeTeamSelector,
                    UsersTeam = usersTeamSelector
                };

                // Fetch last build order entry asynchronously in background (fire-and-forget)
                _ = PopulateLastBuildOrderAsync(lobby, configuration);

                // AdditionalData will be lazily initialized when needed
                return lobby;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Exception in CreateLobby: {ex}");
                throw new InvalidOperationException($"Failed to parse lobby data: {ex.Message}", ex);
            }
        }

        private async Task PopulateLastBuildOrderAsync(GameLobby lobby, AppSettings? configuration)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] PopulateLastBuildOrderAsync started");
            try
            {
                if (configuration?.Replays.Folder == null || lobby.OppositeTeam == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Replays folder or OppositeTeam is null, skipping");
                    return;
                }

                // Get the first player of the opposite team
                var oppositeTeamPlayers = lobby.OppositeTeam(lobby).Players;
                if (oppositeTeamPlayers.Count == 0)
                    return;

                var oppositePlayer = oppositeTeamPlayers.First();
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Fetching build order for opponent: {oppositePlayer.Tag}");

                // Add timeout to prevent blocking if folder scan is slow
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Scanning replays folder: {configuration.Replays.Folder}");
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
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order populated, lastEntry: {lastEntry?.TimeSeconds}s");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order lookup timed out (10s limit exceeded)");
                // Build order lookup timed out, continue without it
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Exception in PopulateLastBuildOrderAsync: {ex}");
                // Other errors, continue without build order
            }
        }

        private MatchCollection ExtractPlayerMatches(byte[] bytes)
        {
            var byteString = new string([.. bytes.Select(@byte => (char)@byte)]);
            return PlayerNamePattern.Matches(byteString);
        }

        private void ValidateLobbyFormat(MatchCollection playerMatches)
        {
            // For 1v1, we need exactly 6 matches: 3 for team 1, 3 for team 2
            // Each team: [name, unknown, tag] = 6 total
            // Indices used: 0(name1), 2(tag1), 3(name2), 5(tag2) + 2 extras
            if (playerMatches.Count != 6)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Invalid match count. Expected 6, found {playerMatches.Count}. Matches: {string.Join(", ", playerMatches.Cast<Match>().Select(m => m.Value))}");
                throw new InvalidOperationException($"Expected 6 player name matches for 1v1, but found {playerMatches.Count}. Unsupported lobby format or player count.");
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
