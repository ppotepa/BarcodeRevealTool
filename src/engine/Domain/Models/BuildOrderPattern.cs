using System;
using System.Collections.Generic;

namespace BarcodeRevealTool.Engine.Domain.Models
{
    /// <summary>
    /// Aggregated insight about an opponent's recent build orders.
    /// </summary>
    public record BuildOrderPattern(
        string OpponentTag,
        IReadOnlyList<BuildOrderStep> Steps,
        string MostFrequentBuild,
        DateTime LastAnalyzed);
}
