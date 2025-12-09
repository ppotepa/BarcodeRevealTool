using System;

namespace BarcodeRevealTool.Engine.Domain.Models
{
    /// <summary>
    /// Represents a single match between the user and an opponent.
    /// </summary>
    public record MatchResult(
        string OpponentTag,
        DateTime GameDate,
        string Map,
        string YourRace,
        string OpponentRace,
        bool YouWon);
}
