namespace BarcodeRevealTool.Engine
{
    public class AppSettings
    {
        public User User { get; set; } = new();
        public Replays Replays { get; set; } = new();
        public bool ExposeApi { get; set; }
    }

    public class User
    {
        public string BattleTag { get; set; } = string.Empty;
    }

    public class Replays
    {
        public string Folder { get; set; } = string.Empty;
        public bool Recursive { get; set; } = true;
        public bool ShowLastBuildOrder { get; set; } = true;
    }
}
