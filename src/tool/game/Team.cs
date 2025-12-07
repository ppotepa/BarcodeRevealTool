
namespace BarcodeRevealTool.Game
{
    internal record Team(string name)
    {
        public HashSet<Player> Players { get; init; } = new HashSet<Player>();
        public override string ToString()
            => $"Team: {name}, Players: {string.Join(", ", Players)}";
    }
}
