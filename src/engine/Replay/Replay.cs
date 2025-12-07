namespace BarcodeRevealTool.replay
{
    public class BuildOrder
    {
        private readonly Queue<BuildOrderEntry> entries = new();

        public Queue<BuildOrderEntry> Entries { get; init; }
    }
}
