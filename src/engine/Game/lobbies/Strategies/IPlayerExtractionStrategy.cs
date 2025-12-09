using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies.Strategies
{
    /// <summary>
    /// Strategy for extracting players from lobby bytes based on game type.
    /// </summary>
    public interface IPlayerExtractionStrategy
    {
        /// <summary>
        /// Extracts players from lobby bytes for the specific game type.
        /// </summary>
        /// <param name="lobbyBytes">The raw lobby bytes to parse</param>
        /// <param name="settings">Application settings including configured user tag</param>
        /// <returns>A tuple containing your team and opponent team</returns>
        (Team yourTeam, Team opponentTeam) ExtractPlayers(byte[] lobbyBytes, AppSettings settings);
    }
}
