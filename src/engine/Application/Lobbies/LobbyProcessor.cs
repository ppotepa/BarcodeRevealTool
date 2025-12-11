using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game.Lobbies;
using Serilog;

namespace BarcodeRevealTool.Engine.Application.Lobbies
{
    public class LobbyProcessor : ILobbyProcessor
    {
        private readonly IGameLobbyFactory _factory;
        private readonly IDebugLobbyFactory _debugFactory;
        private readonly AppSettings _settings;
        private readonly ILogger _logger = Log.ForContext<LobbyProcessor>();
        private int _debugFileIndex = 0;

        public LobbyProcessor(IGameLobbyFactory factory, IDebugLobbyFactory debugFactory, AppSettings settings)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _debugFactory = debugFactory ?? throw new ArgumentNullException(nameof(debugFactory));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<ISoloGameLobby?> TryReadLobbyAsync(CancellationToken cancellationToken)
        {
            // Check if debug mode with manual entry is enabled (priority 1)
            if (!string.IsNullOrEmpty(_settings.Debug?.ManualBattleTag) &&
                !string.IsNullOrEmpty(_settings.Debug?.ManualNickname))
            {
                return _debugFactory.CreateDebugLobbyFromManualEntry(
                    _settings.Debug.ManualBattleTag,
                    _settings.Debug.ManualNickname,
                    _settings);
            }

            // Check if debug mode with lobby files is enabled (priority 2)
            if (_settings.Debug?.LobbyFiles?.Count > 0)
            {
                return await TryReadDebugLobbyAsync(cancellationToken);
            }

            // Normal mode: read from real lobby path
            var lobbyPath = GetLobbyPath();
            if (!File.Exists(lobbyPath))
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(lobbyPath, cancellationToken);
            return _factory.CreateLobby(bytes, _settings);
        }

        /// <summary>
        /// Read lobby files in sequence from debug configuration for testing.
        /// Cycles through the configured lobby files.
        /// </summary>
        private async Task<ISoloGameLobby?> TryReadDebugLobbyAsync(CancellationToken cancellationToken)
        {
            var debugFiles = _settings.Debug.LobbyFiles;
            if (debugFiles == null || debugFiles.Count == 0)
            {
                return null;
            }

            // Cycle through debug files
            var filePath = debugFiles[_debugFileIndex % debugFiles.Count];
            _logger.Information("Loading debug lobby file ({Index}/{Total}): {FilePath}",
                _debugFileIndex + 1, debugFiles.Count, filePath);
            _debugFileIndex++;

            if (!File.Exists(filePath))
            {
                _logger.Warning("Debug lobby file not found: {FilePath}", filePath);
                return null;
            }

            try
            {
                var lobby = _debugFactory.TryCreateDebugLobbyFromFile(filePath, _settings);
                if (lobby == null)
                {
                    _logger.Warning("Failed to create debug lobby from file: {FilePath}", filePath);
                }
                return lobby;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to read debug lobby file: {FilePath}", filePath);
                return null;
            }
        }

        private static string GetLobbyPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(appData, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");
        }
    }
}
