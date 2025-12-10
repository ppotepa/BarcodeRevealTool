namespace BarcodeRevealTool.Engine.Domain.Models
{
    public record MatchStatistics(int GamesPlayed, WinRate WinRate, DateTime? LastGame)
    {
        public static MatchStatistics Empty { get; } = new(0, new WinRate(0, 0), null);
    }
}
