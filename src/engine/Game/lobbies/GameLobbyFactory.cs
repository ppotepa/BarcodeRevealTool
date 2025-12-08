using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.game.lobbies;
using BarcodeRevealTool.Replay;
using Sc2Pulse.Models;
using System.Text.RegularExpressions;
using RegexMatch = System.Text.RegularExpressions.Match;

namespace BarcodeRevealTool.Game
{
    /// <summary>
    /// Factory responsible for parsing raw lobby bytes into strongly typed teams.
    /// Uses queue type detection to extract players based on lobby structure pattern.
    /// </summary>
    public class GameLobbyFactory
    {
        private static readonly Regex PlayerNamePattern = new("(?<name>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");
        private readonly IUserIdentificationStrategy _userIdentificationStrategy;

        public GameLobbyFactory(IUserIdentificationStrategy userIdentificationStrategy)
        {
            _userIdentificationStrategy = userIdentificationStrategy ?? throw new ArgumentNullException(nameof(userIdentificationStrategy));
        }

        public IGameLobby CreateLobby(byte[] bytes, AppSettings? configuration)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] CreateLobby called with {bytes.Length} bytes");
            try
            {
                // Detect queue type from SC2 API
                var detectedQueueType = QueueDetectionService.DetectQueueTypeAsync().Result;
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Detected queue type: {detectedQueueType}");

                var playerMatches = ExtractPlayerMatches(bytes);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Extracted {playerMatches.Count} player matches: {string.Join(", ", playerMatches.Cast<RegexMatch>().Select(m => m.Value))}");

                var selectedPlayers = SelectPlayersByQueueType(playerMatches, detectedQueueType);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] After queue-based selection: {selectedPlayers.Count} players: {string.Join(", ", selectedPlayers.Select(m => m.Value))}");

                ValidateLobbyFormat(selectedPlayers);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Lobby format validation passed");

                var team1 = CreateTeam("Team 1", selectedPlayers, team1Index: true);
                var team2 = CreateTeam("Team 2", selectedPlayers, team1Index: false);

                var (usersTeam, oppositeTeam) = _userIdentificationStrategy.DetermineTeams(team1, team2, bytes);

                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Creating lobby with Team1: {team1?.Players.FirstOrDefault()?.Tag}, Team2: {team2?.Players.FirstOrDefault()?.Tag}");
                var lobby = new GameLobby
                {
                    Team1 = team1,
                    Team2 = team2,
                    OppositeTeam = _ => oppositeTeam,
                    UsersTeam = _ => usersTeam
                };

                _ = PopulateLastBuildOrderAsync(lobby, configuration);
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

                var oppositeTeamPlayers = lobby.OppositeTeam(lobby).Players;
                if (oppositeTeamPlayers.Count == 0)
                    return;

                var oppositePlayer = oppositeTeamPlayers.First();
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Fetching build order for opponent: {oppositePlayer.Tag}");

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Scanning replays folder: {configuration.Replays.Folder}");
                var buildOrder = await BuildOrderReader.Read(
                    configuration.Replays.Folder,
                    oppositePlayer.Tag,
                    configuration.Replays.Recursive,
                    lobby.OppositeTeam
                );

                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order has {buildOrder.Entries.Count} entries");
                var lastEntry = buildOrder.Entries
                    .OrderByDescending(e => e.TimeSeconds)
                    .FirstOrDefault();

                lobby.LastBuildOrderEntry = lastEntry;
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order populated, lastEntry: {lastEntry?.Name} @ {lastEntry?.TimeSeconds}s");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Build order lookup timed out (10s limit exceeded)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Exception in PopulateLastBuildOrderAsync: {ex}");
            }
        }

        private MatchCollection ExtractPlayerMatches(byte[] bytes)
        {
            var byteString = new string([.. bytes.Select(@byte => (char)@byte)]);
            return PlayerNamePattern.Matches(byteString);
        }

        private List<RegexMatch> SelectPlayersByQueueType(MatchCollection playerMatches, Queue? detectedQueueType)
        {
            var matchList = playerMatches.Cast<RegexMatch>().ToList();

            // Extract players based on detected queue type and known binary pattern:
            // For 1v1 (LOTV_1V1):
            //   [0]: Player1 NickName (duplicate)
            //   [1]: Player1 NickName (duplicate)
            //   [2]: Player1 BattleTag ← SELECT
            //   [3]: Player2 NickName (duplicate)
            //   [4]: Player2 NickName (duplicate)
            //   [5]: Player2 BattleTag ← SELECT

            // For other queue types: fall back to deduplication

            if (detectedQueueType == Queue.LOTV_1V1 && matchList.Count >= 6)
            {
                // Use indices [2] and [5] for 1v1 (the actual BattleTags)
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.SelectPlayersByQueueType] 1v1 detected, extracting from indices [2] and [5]");
                var selected = new List<RegexMatch>
                {
                    matchList[2],  // Player 1 BattleTag
                    matchList[5]   // Player 2 BattleTag
                };
                return selected;
            }

            // Fallback: Deduplicate by full tag for unknown queue types
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.SelectPlayersByQueueType] Queue type {detectedQueueType} not specifically handled, using fallback deduplication");
            return DeduplicatePlayers(matchList);
        }
        private List<RegexMatch> DeduplicatePlayers(List<RegexMatch> playerMatches)
        {
            // Simple deduplication: keep first occurrence of each unique tag
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var deduped = new List<RegexMatch>();

            foreach (var match in playerMatches)
            {
                var tag = match.Groups["name"].Value;
                if (string.IsNullOrEmpty(tag))
                    continue;

                if (!seen.Contains(tag))
                {
                    seen.Add(tag);
                    deduped.Add(match);
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.DeduplicatePlayers] Added unique player: {tag}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.DeduplicatePlayers] Skipped duplicate: {tag}");
                }
            }

            // For 1v1 lobbies, take only first 2 unique players
            if (deduped.Count > 2)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.DeduplicatePlayers] Found {deduped.Count} unique players, taking only first 2 for 1v1");
                deduped = deduped.Take(2).ToList();
            }

            return deduped;
        }

        private void ValidateLobbyFormat(List<RegexMatch> playerMatches)
        {
            if (playerMatches.Count < 2)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Invalid match count. Expected at least 2 unique players, found {playerMatches.Count}. Matches: {string.Join(", ", playerMatches.Select(m => m.Value))}");
                throw new InvalidOperationException($"Expected at least 2 unique player names for 1v1, but found {playerMatches.Count}. Unsupported lobby format or player count.");
            }
        }

        private Team CreateTeam(string teamName, List<RegexMatch> playerMatches, bool team1Index)
        {
            int playerIndex = team1Index ? 0 : 1;

            if (playerIndex >= playerMatches.Count)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Index out of bounds: playerIdx={playerIndex}, total unique players={playerMatches.Count}");
                throw new InvalidOperationException($"Failed to extract player at index {playerIndex} from {playerMatches.Count} unique players");
            }

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] CreateTeam {teamName}: using player[{playerIndex}]={playerMatches[playerIndex].Value}");

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
    }
}
