namespace BarcodeRevealTool.Engine.Config
{
    public class AppSettings
    {
        public UserSettings User { get; set; } = new();
        public ReplaySettings Replays { get; set; } = new();
        public DebugSettings Debug { get; set; } = new();
    }

    public class UserSettings
    {
        public string BattleTag { get; set; } = "Player#0000";
    }

    public class ReplaySettings
    {
        public string Folder { get; set; } = string.Empty;
        public bool Recursive { get; set; } = true;
        public int MatchHistoryLimit { get; set; } = 5;
    }

    public class DebugSettings
    {
        /// <summary>
        /// Folder containing lobby files to test (debug/lobbies/).
        /// If set, will cycle through files in this folder instead of reading real lobby.
        /// </summary>
        public string LobbiesFolder { get; set; } = string.Empty;

        /// <summary>
        /// Manual battle tag entry for quick testing (e.g., "Player#1234").
        /// If set, bypasses player extraction from lobby and uses this directly.
        /// Only works in debug mode.
        /// </summary>
        public string ManualBattleTag { get; set; } = string.Empty;

        /// <summary>
        /// Manual nickname entry for quick testing (e.g., "Opponent").
        /// If set, bypasses player extraction from lobby and uses this directly.
        /// Only works in debug mode.
        /// </summary>
        public string ManualNickname { get; set; } = string.Empty;

        /// <summary>
        /// Loaded list of lobby files from LobbiesFolder.
        /// Populated at startup, cycled through when debug mode is active.
        /// </summary>
        public List<string> LobbyFiles { get; set; } = new();
    }
}
