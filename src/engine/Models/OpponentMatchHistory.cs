namespace BarcodeRevealTool.Models
{
    /// <summary>
    /// Represents a past match against an opponent.
    /// </summary>
    public class OpponentMatchHistory
    {
        public string OpponentName { get; set; } = string.Empty;
        public DateTime GameDate { get; set; }
        public string Map { get; set; } = string.Empty;
        public string YourRace { get; set; } = string.Empty;
        public string OpponentRace { get; set; } = string.Empty;
        public string ReplayFileName { get; set; } = string.Empty;
        public int DaysSinceMatch => (int)(DateTime.Now - GameDate).TotalDays;
    }
}
