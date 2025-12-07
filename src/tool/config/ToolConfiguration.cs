public class AppSettings
{
    public UserSettings User { get; set; } = new();
    public ReplaysSettings Replays { get; set; } = new();

    public int RefreshInterval { get; set; }
    public bool ExposeApi { get; set; }
}

public class UserSettings
{
    public string BattleTag { get; set; } = string.Empty;
}

public class ReplaysSettings
{
    public string Folder { get; set; } = string.Empty;
    public bool Recursive { get; set; }
}