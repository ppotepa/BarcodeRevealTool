namespace BarcodeRevealTool.Engine.Domain.Models
{
    public record WinRate(int Wins, int Losses)
    {
        public int TotalGames => Wins + Losses;
        public double Percentage => TotalGames == 0 ? 0 : Wins / (double)TotalGames * 100d;
        public string Display => TotalGames == 0 ? "N/A" : $"{Percentage:F1}%";
    }
}
