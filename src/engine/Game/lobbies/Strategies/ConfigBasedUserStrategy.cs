using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Game;

namespace BarcodeRevealTool.Game.Lobbies.Strategies
{
    /// <summary>
    /// Identifies the user exclusively via the configured battle tag.
    /// </summary>
    public class ConfigBasedUserStrategy : IUserIdentificationStrategy
    {
        private readonly string _normalizedBattleTag;

        public ConfigBasedUserStrategy(AppSettings appSettings)
        {
            if (string.IsNullOrWhiteSpace(appSettings?.User?.BattleTag))
            {
                throw new InvalidOperationException(
                    "ConfigBasedUserStrategy requires 'barcodeReveal:user:battleTag' to be set in appsettings.json.");
            }

            _normalizedBattleTag = NormalizeTag(appSettings.User.BattleTag);
        }

        public (Team userTeam, Team oppositeTeam) DetermineTeams(Team team1, Team team2, byte[] lobbyData)
        {
            ArgumentNullException.ThrowIfNull(team1);
            ArgumentNullException.ThrowIfNull(team2);

            var player1 = team1.Players.FirstOrDefault()
                ?? throw new InvalidOperationException("Team1 contains no players.");
            var player2 = team2.Players.FirstOrDefault()
                ?? throw new InvalidOperationException("Team2 contains no players.");

            var player1Tag = NormalizeTag(player1.Tag ?? player1.NickName ?? string.Empty);
            var player2Tag = NormalizeTag(player2.Tag ?? player2.NickName ?? string.Empty);

            if (string.Equals(player1Tag, _normalizedBattleTag, StringComparison.OrdinalIgnoreCase))
            {
                return (team1, team2);
            }

            if (string.Equals(player2Tag, _normalizedBattleTag, StringComparison.OrdinalIgnoreCase))
            {
                return (team2, team1);
            }

            throw new InvalidOperationException(
                $"Configured battle tag '{_normalizedBattleTag}' was not found in the current lobby (players: {player1Tag}, {player2Tag}).");
        }

        private static string NormalizeTag(string? tag)
            => string.IsNullOrWhiteSpace(tag) ? string.Empty : tag.Replace('_', '#').Trim();
    }
}
