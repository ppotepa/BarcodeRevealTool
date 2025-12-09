namespace BarcodeRevealTool.Engine.Config
{
    public class AppSettings
    {
        public UserSettings User { get; set; } = new();
        public ReplaySettings Replays { get; set; } = new();
    }

    public class UserSettings
    {
        public string BattleTag { get; set; } = "Player#0000";
    }

    public class ReplaySettings
    {
        public string Folder { get; set; } = string.Empty;
        public bool Recursive { get; set; } = true;
    }
}
