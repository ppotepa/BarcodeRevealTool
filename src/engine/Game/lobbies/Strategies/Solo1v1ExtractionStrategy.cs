using BarcodeRevealTool.Engine.Config;
using System.Text;
using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Engine.Game.Lobbies.Strategies
{
    /// <summary>
    /// Strategy for extracting players from 1v1 solo queue lobbies.
    /// </summary>
    public class Solo1v1ExtractionStrategy : IPlayerExtractionStrategy
    {
        private static readonly Regex PlayerPattern = new("(?<name>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})", RegexOptions.Compiled);

        public (Team yourTeam, Team opponentTeam) ExtractPlayers(byte[] lobbyBytes, AppSettings settings)
        {
            var configuredTag = settings.User?.BattleTag ?? "Player#0000";
            var normalizedUser = NormalizeTag(configuredTag);

            if (lobbyBytes is null || lobbyBytes.Length == 0)
            {
                return CreateDefaultTeams(configuredTag);
            }

            var lobbyText = Encoding.UTF8.GetString(lobbyBytes);
            var battleTags = ExtractBattleTags(lobbyText, normalizedUser);

            var yourTag = battleTags
                .FirstOrDefault(tag => NormalizeTag(tag).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase))
                ?? configuredTag;

            var opponentTag = battleTags
                .FirstOrDefault(tag => !NormalizeTag(tag).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase))
                ?? ExtractOpponentFromTokens(lobbyText, normalizedUser)
                ?? "UnknownOpponent#0000";

            var yourTeam = new Team();
            yourTeam.Players.Add(CreatePlayerFromTag(yourTag));

            var opponentTeam = new Team();
            opponentTeam.Players.Add(CreatePlayerFromTag(opponentTag));

            return (yourTeam, opponentTeam);
        }

        private static (Team yourTeam, Team opponentTeam) CreateDefaultTeams(string configuredTag)
        {
            var yourTeam = new Team();
            yourTeam.Players.Add(CreatePlayerFromTag(configuredTag));

            var opponentTeam = new Team();
            opponentTeam.Players.Add(CreatePlayerFromTag("UnknownOpponent#0000"));

            return (yourTeam, opponentTeam);
        }

        private static string? ExtractOpponentFromTokens(string payload, string normalizedUser)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return null;
            }

            var tokens = payload.Split(new[] { '|', '\0', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                {
                    continue;
                }

                if (!PlayerPattern.IsMatch(trimmed))
                {
                    continue;
                }

                if (!NormalizeTag(trimmed).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed;
                }
            }

            return null;
        }

        private static IReadOnlyList<string> ExtractBattleTags(string lobbyText, string normalizedUser)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(lobbyText))
            {
                return results;
            }

            var chunk = new List<string>(3);
            foreach (Match match in PlayerPattern.Matches(lobbyText))
            {
                var tag = match.Groups["name"].Value;
                if (string.IsNullOrWhiteSpace(tag))
                {
                    continue;
                }

                chunk.Add(tag);

                if (chunk.Count == 3)
                {
                    AppendChunk(results, chunk, normalizedUser);
                    chunk.Clear();
                }
            }

            if (chunk.Count > 0)
            {
                AppendChunk(results, chunk, normalizedUser);
            }

            return results;
        }

        private static void AppendChunk(List<string> results, List<string> chunk, string normalizedUser)
        {
            if (chunk.Count == 0)
            {
                return;
            }

            // Prefer whichever entry matches the configured user, otherwise use the last token (actual BattleTag).
            var preferred = chunk.Last();
            foreach (var tag in chunk)
            {
                if (NormalizeTag(tag).Equals(normalizedUser, StringComparison.OrdinalIgnoreCase))
                {
                    preferred = tag;
                    break;
                }
            }

            var normalizedPreferred = NormalizeTag(preferred);
            if (results.Any(existing =>
                    NormalizeTag(existing).Equals(normalizedPreferred, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            results.Add(preferred);
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
