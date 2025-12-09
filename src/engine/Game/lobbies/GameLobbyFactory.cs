using System;
using System.Linq;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Game.Lobbies;
using BarcodeRevealTool.Engine.Game.Lobbies.Strategies;
using Serilog;

namespace BarcodeRevealTool.Engine.Game
{
    /// <summary>
    /// Parses lobby binaries by extracting battle tags and creating appropriate lobby types.
    /// Detects game type (1v1, 2v2, 3v3, 4v4) via SC2 localhost service and creates matching lobby structure.
    /// Uses specialized strategies for each game type to extract players.
    /// </summary>
    public class GameLobbyFactory : IGameLobbyFactory
    {
        private static readonly ILogger Logger = Log.ForContext<GameLobbyFactory>();

        public ISoloGameLobby? CreateLobby(byte[] lobbyBytes, AppSettings settings)
        {
            if (lobbyBytes is null || lobbyBytes.Length == 0)
            {
                return null;
            }

            // Synchronously detect game type (blocking, but necessary for lobby creation)
            var gameType = DetectGameTypeSynchronous();
            Logger.Debug("Detected game type: {GameType}", gameType);

            // Extract players based on detected game type using specialized strategies
            var (yourTeam, opponentTeam) = ExtractPlayersByGameType(lobbyBytes, settings, gameType);

            // Create appropriate lobby structure based on game type
            return CreateLobbyByGameType(gameType, yourTeam, opponentTeam);
        }

        /// <summary>
        /// Extract players based on queue type using specialized strategies.
        /// Each strategy handles the specific format for its game type.
        /// </summary>
        private static (Team yourTeam, Team opponentTeam) ExtractPlayersByGameType(
            byte[] lobbyBytes, AppSettings settings, GameType? gameType)
        {
            var strategy = gameType switch
            {
                GameType.Solo1v1 => (IPlayerExtractionStrategy)new Solo1v1ExtractionStrategy(),
                GameType.Team2v2 => (IPlayerExtractionStrategy)new Team2v2ExtractionStrategy(),
                GameType.Team3v3 => (IPlayerExtractionStrategy)new Team3v3ExtractionStrategy(),
                GameType.Team4v4 => (IPlayerExtractionStrategy)new Team4v4ExtractionStrategy(),
                _ => (IPlayerExtractionStrategy)new Solo1v1ExtractionStrategy() // Fallback to 1v1
            };

            return strategy.ExtractPlayers(lobbyBytes, settings);
        }

        /// <summary>
        /// Synchronously detect game type by running async task on thread pool.
        /// Used because CreateLobby is synchronous but detection is async.
        /// </summary>
        private static GameType? DetectGameTypeSynchronous()
        {
            try
            {
                var task = GameTypeDetector.DetectGameTypeAsync();
                task.Wait(TimeSpan.FromSeconds(5));
                return task.Result;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to detect game type");
                return null;
            }
        }

        /// <summary>
        /// Create lobby object based on detected game type with appropriate player assignment.
        /// </summary>
        private static ISoloGameLobby CreateLobbyByGameType(GameType? gameType, Team yourTeam, Team opponentTeam)
        {
            return gameType switch
            {
                GameType.Solo1v1 => CreateSolo1v1Lobby(yourTeam, opponentTeam),
                GameType.Team2v2 => CreateTeam2v2Lobby(yourTeam, opponentTeam),
                GameType.Team3v3 => CreateTeam3v3Lobby(yourTeam, opponentTeam),
                GameType.Team4v4 => CreateTeam4v4Lobby(yourTeam, opponentTeam),
                _ => CreateSolo1v1Lobby(yourTeam, opponentTeam) // Fallback to 1v1
            };
        }

        /// <summary>
        /// Create a 1v1 solo lobby (you vs opponent, one player per team).
        /// </summary>
        private static ISoloGameLobby CreateSolo1v1Lobby(Team yourTeam, Team opponentTeam)
        {
            Logger.Debug("Creating Solo 1v1 lobby");
            var opponentTag = opponentTeam.Players.FirstOrDefault()?.Tag ?? "unknown#0000";
            return new Lobbies.GameLobby(yourTeam, opponentTeam, opponentTag);
        }

        /// <summary>
        /// Create a 2v2 team lobby (you + ally vs 2 opponents).
        /// </summary>
        private static ISoloGameLobby CreateTeam2v2Lobby(Team yourTeam, Team opponentTeam)
        {
            Logger.Debug("Creating Team 2v2 lobby");
            var opponentTag = opponentTeam.Players.FirstOrDefault()?.Tag ?? "unknown#0000";
            return new Lobbies.GameLobby(yourTeam, opponentTeam, opponentTag);
        }

        /// <summary>
        /// Create a 3v3 team lobby (you + 2 allies vs 3 opponents).
        /// </summary>
        private static ISoloGameLobby CreateTeam3v3Lobby(Team yourTeam, Team opponentTeam)
        {
            Logger.Debug("Creating Team 3v3 lobby");
            var opponentTag = opponentTeam.Players.FirstOrDefault()?.Tag ?? "unknown#0000";
            return new Lobbies.GameLobby(yourTeam, opponentTeam, opponentTag);
        }

        /// <summary>
        /// Create a 4v4 team lobby (you + 3 allies vs 4 opponents).
        /// </summary>
        private static ISoloGameLobby CreateTeam4v4Lobby(Team yourTeam, Team opponentTeam)
        {
            Logger.Debug("Creating Team 4v4 lobby");
            var opponentTag = opponentTeam.Players.FirstOrDefault()?.Tag ?? "unknown#0000";
            return new Lobbies.GameLobby(yourTeam, opponentTeam, opponentTag);
        }
    }
}
