using System;

namespace BarcodeRevealTool.Engine.Domain.Models
{
    /// <summary>
    /// Represents a single action in a build order timeline.
    /// </summary>
    public record BuildOrderStep(double TimeSeconds, string Kind, string Name)
    {
        public TimeSpan Time => TimeSpan.FromSeconds(TimeSeconds);
    }
}
