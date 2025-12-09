using System;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies.Strategies
{
    /// <summary>
    /// Strategy for extracting players from 3v3 team queue lobbies.
    /// Currently not implemented.
    /// </summary>
    public class Team3v3ExtractionStrategy : IPlayerExtractionStrategy
    {
        public (Team yourTeam, Team opponentTeam) ExtractPlayers(byte[] lobbyBytes, AppSettings settings)
        {
            throw new NotImplementedException("3v3 team queue player extraction is not yet implemented.");
        }
    }
}
