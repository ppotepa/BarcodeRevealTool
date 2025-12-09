namespace BarcodeRevealTool.Engine.Application
{
    public enum ToolState
    {
        Awaiting,
        InGame
    }

    public sealed class ToolStateChangedEventArgs : EventArgs
    {
        public ToolStateChangedEventArgs(ToolState previous, ToolState current)
        {
            Previous = previous;
            Current = current;
        }

        public ToolState Previous { get; }
        public ToolState Current { get; }
    }
}
