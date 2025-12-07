namespace BarcodeRevealTool.Engine
{
    public class AppSettings
    {
        public User? User { get; set; }
        public Replays? Replays { get; set; }
    }

    public class User
    {
        public string? BattleTag { get; set; }
    }

    public class Replays
    {
        public string? Folder { get; set; }
        public bool Recursive { get; set; } = true;
    }
}
