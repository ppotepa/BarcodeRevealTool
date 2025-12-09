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
    /// Represents extracted player information from lobby.
    /// </summary>
    public record LobbyPlayerInfo(
        string Player1Nickname,
        string Player1Tag,
        string Player2Nickname,
        string Player2Tag,
        Queue? CreationStrategy = null
    );

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
                var playerInfo = ExtractPlayerInfo(bytes);
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Extracted players: Player1={playerInfo.Player1Nickname}({playerInfo.Player1Tag}), Player2={playerInfo.Player2Nickname}({playerInfo.Player2Tag}), CreationStrategy={playerInfo.CreationStrategy}");

                // For now, only support 1v1. Other queue types will be handled when lobby creation is extended.
                if (playerInfo.CreationStrategy != Queue.LOTV_1V1)
                {
                    System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory] Warning: Detected queue type {playerInfo.CreationStrategy} is not yet fully supported. Currently only LOTV_1V1 is implemented. Proceeding with extracted players as a 1v1.");
                }

                var team1 = CreateTeam("Team 1", playerInfo.Player1Nickname, playerInfo.Player1Tag);
                var team2 = CreateTeam("Team 2", playerInfo.Player2Nickname, playerInfo.Player2Tag);

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

        private LobbyPlayerInfo ExtractPlayerInfo(byte[] bytes)
        {
            var byteString = new string([.. bytes.Select(@byte => (char)@byte)]);
            var matches = PlayerNamePattern.Matches(byteString);
            var matchList = matches.Cast<RegexMatch>().ToList();

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.ExtractPlayerInfo] Found {matchList.Count} total matches: {string.Join(", ", matchList.Select(m => m.Value))}");

            // Detect queue type and extract player info based on number of matches:
            // 1v1: 6 matches with pattern:
            //   [0]: Player1 Nickname
            //   [1]: Player1 Nickname (duplicate)
            //   [2]: Player1 BattleTag ← SELECT
            //   [3]: Player2 Nickname
            //   [4]: Player2 Nickname (duplicate)
            //   [5]: Player2 BattleTag ← SELECT

            Queue? creationStrategy = null;
            if (matchList.Count < 6)
            {
                throw new InvalidOperationException($"Expected at least 6 player name matches for lobby (nicknames + tags), but found {matchList.Count}");
            }

            // Determine queue type based on match count
            int playerCount = matchList.Count / 3;
            if (playerCount == 2 && matchList.Count == 6)
            {
                creationStrategy = Queue.LOTV_1V1;
            }
            else if (playerCount == 4 && matchList.Count == 12)
            {
                creationStrategy = Queue.LOTV_2V2;
            }
            else if (playerCount == 6 && matchList.Count == 18)
            {
                creationStrategy = Queue.LOTV_3V3;
            }
            else if (playerCount == 8 && matchList.Count == 24)
            {
                creationStrategy = Queue.LOTV_4V4;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.ExtractPlayerInfo] Warning: Could not determine queue type from {matchList.Count} matches ({playerCount} players)");
            }

            // Extract players from indices:
            // Player 1: nickname at [0], tag at [2]
            // Player 2: nickname at [3], tag at [5]
            var player1Nickname = matchList[0].Groups["name"].Value;
            var player1Tag = matchList[2].Groups["name"].Value;

            var player2Nickname = matchList[3].Groups["name"].Value;
            var player2Tag = matchList[5].Groups["name"].Value;

            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.ExtractPlayerInfo] Extracted: P1Nick={player1Nickname}, P1Tag={player1Tag}, P2Nick={player2Nickname}, P2Tag={player2Tag}, CreationStrategy={creationStrategy}");

            return new LobbyPlayerInfo(player1Nickname, player1Tag, player2Nickname, player2Tag, creationStrategy);
        }

        private Team CreateTeam(string teamName, string nickname, string tag)
        {
            System.Diagnostics.Debug.WriteLine($"[GameLobbyFactory.CreateTeam] {teamName}: NickName={nickname}, Tag={tag}");

            var player = new Player
            {
                NickName = nickname,
                Tag = tag
            };

            return new(teamName) { Players = [player] };
        }
    }
}
