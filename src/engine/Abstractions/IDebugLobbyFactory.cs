using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game.Lobbies;

namespace BarcodeRevealTool.Engine.Abstractions
{
    /// <summary>
    /// Factory for creating debug lobbies from manual player entry or test files.
    /// Used to speed up debugging by allowing manual opponent specification without parsing actual lobby files.
    /// </summary>
    public interface IDebugLobbyFactory
    {
        /// <summary>
        /// Create a lobby from manually entered opponent details.
        /// </summary>
        ISoloGameLobby CreateDebugLobbyFromManualEntry(string opponentBattleTag, string opponentNickname, AppSettings settings);

        /// <summary>
        /// Try to create a lobby from a debug lobby file.
        /// </summary>
        ISoloGameLobby? TryCreateDebugLobbyFromFile(string filePath, AppSettings settings);
    }
}
