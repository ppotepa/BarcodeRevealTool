using System;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies.Strategies
{
    /// <summary>
    /// Strategy for extracting players from 4v4 team queue lobbies.
    /// Currently not implemented.
    /// </summary>
    public class Team4v4ExtractionStrategy : IPlayerExtractionStrategy
    {
        public (Team yourTeam, Team opponentTeam) ExtractPlayers(byte[] lobbyBytes, AppSettings settings)
        {
            throw new NotImplementedException("4v4 team queue player extraction is not yet implemented.");
        }
    }
}
