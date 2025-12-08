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
            if (string.IsNullOrEmpty(userBattleTag))
            {
                // Fallback if no user tag - return first team
                return isUsers ? team1! : team2!;
            }

            // Prepare normalized versions for robust comparison
            string? namePrefix = ExtractNamePrefix(userBattleTag); // e.g., "Ignacy" from "Ignacy_236"
            string displayBattleTag = userBattleTag.Replace('_', '#');
            string underscoreBattleTag = userBattleTag.Replace('#', '_');

            // Debug: list players in both teams
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] FindTeam: userBattleTag='{userBattleTag}', display='{displayBattleTag}', prefix='{namePrefix}'");
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Team1 players: {string.Join(", ", team1?.Players.Select(p => p.Tag + " (" + p.NickName + ")") ?? new string[0])}");
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Team2 players: {string.Join(", ", team2?.Players.Select(p => p.Tag + " (" + p.NickName + ")") ?? new string[0])}");

            bool IsUserMatch(Player p)
            {
                if (p == null) return false;
                var pTag = p.Tag ?? string.Empty;
                var pNick = p.NickName ?? string.Empty;

                var pTagDisplay = pTag.Replace('_', '#');
                var pTagUnderscore = pTag.Replace('#', '_');

                // Exact matches (either stored form or normalized forms)
                if (string.Equals(pTag, userBattleTag, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(pTagDisplay, displayBattleTag, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(pTagUnderscore, underscoreBattleTag, StringComparison.OrdinalIgnoreCase)) return true;

                // Match by nickname (display form)
                if (string.Equals(pNick, displayBattleTag, StringComparison.OrdinalIgnoreCase)) return true;

                // Match by name prefix (loose match) as a last resort
                if (!string.IsNullOrEmpty(namePrefix) && pNick.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)) return true;

                return false;
            }

            var userInTeam1 = team1?.Players.Any(p => IsUserMatch(p)) ?? false;

            var userTeam = userInTeam1 ? team1 : team2;
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] User assigned to {(userInTeam1 ? "Team1" : "Team2")}");
            return isUsers ? userTeam! : (userTeam == team1 ? team2 : team1)!;
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
