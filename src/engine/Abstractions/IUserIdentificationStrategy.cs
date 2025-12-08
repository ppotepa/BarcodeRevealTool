using BarcodeRevealTool.Game;

namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Strategy abstraction for determining which lobby team belongs to the user.
    /// </summary>
    public interface IUserIdentificationStrategy
    {
        /// <summary>
        /// Determine the user and opponent teams for the given lobby snapshot.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the strategy cannot identify the configured user in the lobby.
        /// </exception>
        (Team userTeam, Team oppositeTeam) DetermineTeams(Team team1, Team team2, byte[] lobbyData);
    }
}
