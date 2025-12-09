using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies.Strategies
{
    /// <summary>
    /// Strategy for extracting players from 1v1 solo queue lobbies.
    /// </summary>
    public class Solo1v1ExtractionStrategy : IPlayerExtractionStrategy
    {
        private static readonly Regex PlayerPattern = new("(?<name>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");

        public (Team yourTeam, Team opponentTeam) ExtractPlayers(byte[] lobbyBytes, AppSettings settings)
        {
            if (lobbyBytes is null || lobbyBytes.Length == 0)
            {
                var defaultTag = settings.User?.BattleTag ?? "Player#0000";
                var yourTeam = new Team();
                yourTeam.Players.Add(new Player
                {
                    NickName = defaultTag.Split('#').FirstOrDefault() ?? "Player",
                    Tag = defaultTag,
                    Race = "Unknown"
                });
                var opponentTeam = new Team();
                opponentTeam.Players.Add(new Player
                {
                    NickName = "UnknownOpponent",
                    Tag = "UnknownOpponent#0000",
                    Race = "Unknown"
                });
                return (yourTeam, opponentTeam);
            }

            var lobbyText = new string(lobbyBytes.Select(b => (char)b).ToArray());
            var allMatches = PlayerPattern.Matches(lobbyText)
                .Cast<Match>()
                .Select(m => m.Groups["name"].Value)
                .ToList();

            // Deduplicate matches while preserving order
            var distinctMatches = allMatches
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var configuredTag = settings.User?.BattleTag ?? "Player#0000";

            // Initialize teams
            var yourTeam_result = new Team();
            var opponentTeam_result = new Team();

            if (distinctMatches.Count >= 4)
            {
                // Format: [Nick1, Tag1, Nick2, Tag2]
                var player1Nick = distinctMatches[0];
                var player1Tag = distinctMatches[1];
                var player2Nick = distinctMatches[2];
                var player2Tag = distinctMatches[3];

                var normalizedUser = NormalizeTag(configuredTag);

                // Check which player tag matches the configured user
                var isPlayer1TheUser = NormalizeTag(player1Tag).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase);
                var isPlayer2TheUser = NormalizeTag(player2Tag).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase);

                if (isPlayer1TheUser)
                {
                    // Player 1 is you, Player 2 is opponent
                    yourTeam_result.Players.Add(CreatePlayerFromTag(player1Nick, player1Tag));
                    opponentTeam_result.Players.Add(CreatePlayerFromTag(player2Nick, player2Tag));
                }
                else if (isPlayer2TheUser)
                {
                    // Player 2 is you, Player 1 is opponent
                    yourTeam_result.Players.Add(CreatePlayerFromTag(player2Nick, player2Tag));
                    opponentTeam_result.Players.Add(CreatePlayerFromTag(player1Nick, player1Tag));
                }
                else
                {
                    // User not found, use first as user
                    yourTeam_result.Players.Add(CreatePlayerFromTag(player1Nick, player1Tag));
                    opponentTeam_result.Players.Add(CreatePlayerFromTag(player2Nick, player2Tag));
                }
            }
            else if (distinctMatches.Count >= 2)
            {
                // Fallback: treat as simple tags without separation
                var normalizedUser = NormalizeTag(configuredTag);

                var userTag = distinctMatches.FirstOrDefault(m =>
                        NormalizeTag(m).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(userTag))
                {
                    var opponentTag = distinctMatches.FirstOrDefault(m =>
                            !NormalizeTag(m).Equals(NormalizeTag(userTag), StringComparison.OrdinalIgnoreCase));

                    yourTeam_result.Players.Add(CreatePlayerFromTag(userTag));
                    opponentTeam_result.Players.Add(CreatePlayerFromTag(opponentTag ?? distinctMatches.Last()));
                }
                else
                {
                    yourTeam_result.Players.Add(CreatePlayerFromTag(distinctMatches[0]));
                    opponentTeam_result.Players.Add(CreatePlayerFromTag(distinctMatches[1]));
                }
            }
            else
            {
                // Fallback to simple token parsing
                var payload = Encoding.UTF8.GetString(lobbyBytes);
                var tokens = payload.Split('|');

                var yourTag_fallback = tokens.ElementAtOrDefault(0) ?? configuredTag;
                var opponentTag_fallback = tokens.ElementAtOrDefault(2) ?? "UnknownOpponent#0000";

                yourTeam_result.Players.Add(CreatePlayerFromTag(yourTag_fallback));
                opponentTeam_result.Players.Add(CreatePlayerFromTag(opponentTag_fallback));
            }
            return (yourTeam_result, opponentTeam_result);
        }

        private static Player CreatePlayerFromTag(string nick, string tag)
        {
            return new Player
            {
                NickName = nick,
                Tag = tag,
                Race = "Unknown"
            };
        }

        private static Player CreatePlayerFromTag(string tag)
        {
            var nickname = tag.Split('#').FirstOrDefault() ?? "Player";
            return new Player
            {
                NickName = nickname,
                Tag = tag,
                Race = "Unknown"
            };
        }

        private static string NormalizeTag(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Replace('_', '#').Trim();
    }
}
