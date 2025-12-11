using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game;
using BarcodeRevealTool.Engine.Game.Lobbies;
using Serilog;

namespace BarcodeRevealTool.Engine.Application.Lobbies
{
    /// <summary>
    /// Factory for creating debug lobbies for testing without actual SC2 running.
    /// Supports two modes:
    /// 1. Manual entry: Create lobby from user-entered opponent details
    /// 2. File-based: Load lobby from debug lobby files
    /// </summary>
    public class DebugLobbyFactory : IDebugLobbyFactory
    {
        private readonly IGameLobbyFactory _mainFactory;
        private readonly ILogger _logger = Log.ForContext<DebugLobbyFactory>();

        public DebugLobbyFactory(IGameLobbyFactory mainFactory)
        {
            _mainFactory = mainFactory ?? throw new ArgumentNullException(nameof(mainFactory));
        }

        /// <summary>
        /// Create a lobby from manually entered opponent details.
        /// Creates a minimal 1v1 lobby with the provided opponent information.
        /// </summary>
        public ISoloGameLobby CreateDebugLobbyFromManualEntry(string opponentBattleTag, string opponentNickname, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(opponentBattleTag) || string.IsNullOrWhiteSpace(opponentNickname))
            {
                throw new ArgumentException("Opponent battle tag and nickname are required");
            }

            try
            {
                _logger.Information("Creating debug lobby from manual entry: {Nickname} ({BattleTag})", opponentNickname, opponentBattleTag);

                // Create your team
                var yourTeam = new Team();
                yourTeam.Players.Add(new Player
                {
                    NickName = settings.User?.BattleTag ?? "Player",
                    Tag = settings.User?.BattleTag ?? "Player#0000",
                    Race = "Unknown"
                });

                // Create opponent team
                var opponentTeam = new Team();
                opponentTeam.Players.Add(new Player
                {
                    NickName = opponentNickname,
                    Tag = opponentBattleTag,
                    Race = "Unknown"
                });

                // Create the lobby
                var lobby = new GameLobby(yourTeam, opponentTeam, opponentBattleTag);

                _logger.Debug("Debug lobby created successfully: {YourTag} vs {OpponentTag}",
                    yourTeam.Players.First().Tag, opponentBattleTag);

                return lobby;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to create debug lobby from manual entry");
                throw;
            }
        }

        /// <summary>
        /// Try to create a lobby from a debug lobby file.
        /// Falls back to the main factory if successful.
        /// </summary>
        public ISoloGameLobby? TryCreateDebugLobbyFromFile(string filePath, AppSettings settings)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                _logger.Warning("Debug lobby file not found: {FilePath}", filePath);
                return null;
            }

            try
            {
                _logger.Information("Loading debug lobby from file: {FilePath}", filePath);

                var bytes = File.ReadAllBytes(filePath);
                var lobby = _mainFactory.CreateLobby(bytes, settings);

                if (lobby != null)
                {
                    _logger.Debug("Debug lobby created successfully from file: {FilePath}", filePath);
                }
                else
                {
                    _logger.Warning("Failed to parse debug lobby file: {FilePath}", filePath);
                }

                return lobby;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to load debug lobby from file: {FilePath}", filePath);
                return null;
            }
        }
    }
}
