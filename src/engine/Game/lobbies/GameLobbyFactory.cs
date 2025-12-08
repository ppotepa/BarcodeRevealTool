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

                var usersTeam = FindTeam(team1, team2, userBattleTag, isUsers: true);
                var oppositeTeam = FindTeam(team1, team2, userBattleTag, isUsers: false);

                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Creating lobby with Team1: {team1?.Players.FirstOrDefault()?.Tag}, Team2: {team2?.Players.FirstOrDefault()?.Tag}");
                var lobby = new GameLobby()
                {
                    Team1 = team1,
                    Team2 = team2,
                    OppositeTeam = _ => oppositeTeam,
                    UsersTeam = _ => usersTeam
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
                if (configuration?.Replays.ShowLastBuildOrder == false)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] ShowLastBuildOrder is disabled, skipping");
                    return;
                }

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

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] CreateTeam {teamName}: nameIdx={nameMatchIndex}, tagIdx={tagMatchIndex}");
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Match[{nameMatchIndex}]={playerMatches[nameMatchIndex].Value}, Match[{tagMatchIndex}]={playerMatches[tagMatchIndex].Value}");

            var player = new Player
            {
                NickName = playerMatches[nameMatchIndex].Groups["name"].Value,
                Tag = playerMatches[tagMatchIndex].Groups["name"].Value
            };

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Created player: NickName={player.NickName}, Tag={player.Tag}");

            return new(teamName) { Players = [player] };
        }

        private Team FindTeam(Team? team1, Team? team2, string userBattleTag, bool isUsers)
        {
            var hasUser = (Team? team) => team?.Players.Any(p => p.Tag == userBattleTag) ?? false;

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: team1Tag={team1?.Players.FirstOrDefault()?.Tag}, team2Tag={team2?.Players.FirstOrDefault()?.Tag}, userBattleTag={userBattleTag}");
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: team1HasUser={hasUser(team1)}, team2HasUser={hasUser(team2)}");

            var userTeam = hasUser(team1) ? team1 : team2;
            var result = isUsers ? userTeam : (userTeam == team1 ? team2 : team1);
            
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam returning: {result?.Players.FirstOrDefault()?.Tag} (isUsers={isUsers})");
            
            if (result == null)
            {
                throw new InvalidOperationException($"Could not find appropriate team for user {userBattleTag}");
            }
            
            return result;
        }
    }
}
