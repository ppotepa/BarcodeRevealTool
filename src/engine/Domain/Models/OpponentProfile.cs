using System;
using System.Collections.Generic;

namespace BarcodeRevealTool.Engine.Domain.Models
{
    /// <summary>
    /// Snapshot of all information known about an opponent.
    /// </summary>
    public record OpponentProfile(
        string OpponentTag,
        WinRate VersusYou,
        PreferredRaces PreferredRaces,
        IReadOnlyList<string> FavoriteMaps,
        BuildOrderPattern CurrentBuildPattern,
        DateTime LastPlayed);
}
