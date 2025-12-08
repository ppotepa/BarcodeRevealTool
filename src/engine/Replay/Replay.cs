using BarcodeRevealTool.Replay;

namespace BarcodeRevealTool.replay
{
    public class BuildOrder
    {
        private readonly Queue<BuildOrderEntry> entries = new();

        public Queue<BuildOrderEntry> Entries { get; init; }

        /// <summary>
        /// Replay metadata extracted during decode. 
        /// Only populated when BuildOrder is created from full replay decode.
        /// Used to avoid re-reading metadata from disk.
        /// </summary>
        public ReplayMetadata? Metadata { get; init; }
    }
}
