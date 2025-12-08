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
        private readonly ReplayDatabase? _database;

        public GameLobbyFactory(ReplayDatabase? database = null)
        {
            _database = database;
        }

        public IGameLobby CreateLobby(byte[] bytes, AppSettings? configuration)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] CreateLobby called with {bytes.Length} bytes");
            try
            {
                var playerMatches = ExtractPlayerMatches(bytes);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Extracted {playerMatches.Count} player matches: {string.Join(", ", playerMatches.Cast<Match>().Select(m => m.Value))}");

                // Deduplicate matches while preserving order (keep first occurrence of each unique player)
                var uniqueMatches = DeduplicateMatches(playerMatches);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] After deduplication: {uniqueMatches.Count} unique matches: {string.Join(", ", uniqueMatches.Select(m => m.Value))}");

                ValidateLobbyFormat(uniqueMatches);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Lobby format validation passed");

                var team1 = CreateTeam("Team 1", uniqueMatches, team1Index: true);
                var team2 = CreateTeam("Team 2", uniqueMatches, team1Index: false);

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

                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order has {buildOrder.Entries.Count} entries");
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Finding last entry via OrderByDescending...");
                var lastEntry = buildOrder.Entries
                    .OrderByDescending(e => e.TimeSeconds)
                    .FirstOrDefault();

                lobby.LastBuildOrderEntry = lastEntry;
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order populated, lastEntry: {lastEntry?.Name} @ {lastEntry?.TimeSeconds}s");
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

        /// <summary>
        /// Deduplicate player name matches while preserving order.
        /// Keeps the first occurrence of each unique player, removes duplicates.
        /// Example: [wlvm#833, wlvm#833, Ignacy#236, Ignacy#236, XLover#2803] → [wlvm#833, Ignacy#236, XLover#2803]
        /// </summary>
        private List<Match> DeduplicateMatches(MatchCollection playerMatches)
        {
            var seen = new HashSet<string>();
            var deduplicated = new List<Match>();

            foreach (Match match in playerMatches)
            {
                var playerName = match.Value;
                if (!seen.Contains(playerName))
                {
                    seen.Add(playerName);
                    deduplicated.Add(match);
                }
            }

            return deduplicated;
        }

        private void ValidateLobbyFormat(List<Match> playerMatches)
        {
            // For 1v1, we expect at least 2 unique matches (2 players)
            // After deduplication, we should have exactly 2 different players
            if (playerMatches.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Invalid match count. Expected at least 2 unique players, found {playerMatches.Count}. Matches: {string.Join(", ", playerMatches.Select(m => m.Value))}");
                throw new InvalidOperationException($"Expected at least 2 unique player names for 1v1, but found {playerMatches.Count}. Unsupported lobby format or player count.");
            }
        }

        private Team CreateTeam(string teamName, List<Match> playerMatches, bool team1Index)
        {
            // After deduplication, we have exactly 2 unique players
            // Team1: player[0], Team2: player[1]
            int playerIndex = team1Index ? 0 : 1;

            // Validate index is within bounds
            if (playerIndex >= playerMatches.Count)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Index out of bounds: playerIdx={playerIndex}, total unique players={playerMatches.Count}");
                throw new InvalidOperationException($"Failed to extract player at index {playerIndex} from {playerMatches.Count} unique players");
            }

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] CreateTeam {teamName}: using player[{playerIndex}]={playerMatches[playerIndex].Value}");

            // Extract raw nickname and normalize it (replace _ with # for display)
            var rawNickName = playerMatches[playerIndex].Groups["name"].Value;
            var displayNickName = rawNickName.Replace('_', '#');

            var player = new Player
            {
                NickName = displayNickName,
                Tag = playerMatches[playerIndex].Groups["name"].Value
            };

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Created player: NickName={player.NickName} (raw: {rawNickName}), Tag={player.Tag}");

            return new(teamName) { Players = [player] };
        }

        private Team FindTeam(Team? team1, Team? team2, string userBattleTag, bool isUsers)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: checking which lobby player is a known user account");

            if (team1 == null || team2 == null)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: team1 or team2 is null, using fallback");
                return isUsers ? team1! : team2!;
            }

            var player1 = team1.Players.FirstOrDefault();
            var player2 = team2.Players.FirstOrDefault();

            if (player1 == null || player2 == null)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: player1 or player2 is null, using fallback");
                return isUsers ? team1! : team2!;
            }

            var player1Name = player1.NickName ?? string.Empty;
            var player2Name = player2.NickName ?? string.Empty;

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Checking lobby players for known user accounts");
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Team1 player: '{player1Name}', Team2 player: '{player2Name}'");

            try
            {
                // Use injected database or create new one
                var database = _database ?? new ReplayDatabase();

                // Check if either lobby player is a known user account (exact match)
                bool player1IsUser = database.IsKnownUserAccount(player1Name);
                bool player2IsUser = database.IsKnownUserAccount(player2Name);

                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: player1 '{player1Name}' isUser={player1IsUser}, player2 '{player2Name}' isUser={player2IsUser}");

                Team? userTeam = null;

                if (player1IsUser && !player2IsUser)
                {
                    // Player1 is the user
                    userTeam = team1;
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Player1 '{player1Name}' is the known user account");
                }
                else if (player2IsUser && !player1IsUser)
                {
                    // Player2 is the user
                    userTeam = team2;
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Player2 '{player2Name}' is the known user account");
                }
                else if (player1IsUser && player2IsUser)
                {
                    // Both are known users - this shouldn't happen in normal gameplay, use Team1 as primary
                    userTeam = team1;
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Both players are known accounts, using Team1 as primary");
                }
                else
                {
                    // Neither player is a known account - use configured user or fallback
                    if (!string.IsNullOrEmpty(userBattleTag))
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: No exact match found, using configured userBattleTag='{userBattleTag}' as fallback");
                        // Try to match against config user tag
                        var displayBattleTag = userBattleTag.Replace('_', '#');
                        if (string.Equals(player1Name, displayBattleTag, StringComparison.OrdinalIgnoreCase))
                        {
                            userTeam = team1;
                            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Matched Player1 to configured user tag");
                        }
                        else if (string.Equals(player2Name, displayBattleTag, StringComparison.OrdinalIgnoreCase))
                        {
                            userTeam = team2;
                            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Matched Player2 to configured user tag");
                        }
                        else
                        {
                            // No match, use Team1 as default
                            userTeam = team1;
                            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: No match to configured tag, defaulting to Team1");
                        }
                    }
                    else
                    {
                        // No configured user, use Team1 as default
                        userTeam = team1;
                        System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: No configured user, defaulting to Team1");
                    }
                }

                var oppositeTeam = (userTeam == team1) ? team2 : team1;
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: User team = {(userTeam == team1 ? "Team1" : "Team2")}, Opposite team = {(oppositeTeam == team1 ? "Team1" : "Team2")}");
                return isUsers ? userTeam : oppositeTeam;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: Exception during user validation: {ex.Message}, using fallback");
                // Fallback if database check fails
                return isUsers ? team1 : team2;
            }
        }

        /// <summary>
        /// Extract the name prefix from a battle tag.
        /// Example: "Ignacy_236" → "Ignacy"
        /// </summary>
        private static string? ExtractNamePrefix(string battleTag)
        {
            if (string.IsNullOrEmpty(battleTag))
                return null;

            var underscoreIndex = battleTag.IndexOf('_');
            if (underscoreIndex > 0)
            {
                return battleTag.Substring(0, underscoreIndex);
            }

            return null;
        }
    }
}
