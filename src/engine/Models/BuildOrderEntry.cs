namespace BarcodeRevealTool.Engine
{
    /// <summary>
    /// Represents a single build order action/entry
    /// </summary>
    public record BuildOrderEntry(
        int PlayerId,
        double TimeSeconds,
        string Kind,
        string Name
    );
}
