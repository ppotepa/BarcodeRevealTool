using System;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game;

namespace BarcodeRevealTool.Engine.Game.Lobbies.Strategies
{
    /// <summary>
    /// Strategy for extracting players from 2v2 team queue lobbies.
    /// Currently not implemented.
    /// </summary>
    public class Team2v2ExtractionStrategy : IPlayerExtractionStrategy
    {
        public (Team yourTeam, Team opponentTeam) ExtractPlayers(byte[] lobbyBytes, AppSettings settings)
        {
            throw new NotImplementedException("2v2 team queue player extraction is not yet implemented.");
        }
    }
}
