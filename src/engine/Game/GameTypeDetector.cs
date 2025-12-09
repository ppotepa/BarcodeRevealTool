using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace BarcodeRevealTool.Engine.Game
{
    /// <summary>
    /// Detects the current game type (1v1, 2v2, 3v3, 4v4) by querying the SC2 localhost game service.
    /// Communicates with SC2 via http://localhost:6119/game to determine active match type.
    /// </summary>
    public class GameTypeDetector
    {
        private static readonly HttpClient Client = new();
        private const string LocalGameServiceUrl = "http://localhost:6119/game";
        private const int TimeoutSeconds = 5;
        private static readonly ILogger Logger = Log.ForContext<GameTypeDetector>();

        /// <summary>
        /// Detect the current game type by querying SC2 game service.
        /// Returns null if unable to determine or if no game is active.
        /// </summary>
        public static async Task<GameType?> DetectGameTypeAsync()
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(TimeoutSeconds));
                var response = await Client.GetAsync(LocalGameServiceUrl, cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    Logger.Debug("SC2 game service returned {StatusCode}", response.StatusCode);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token);
                var gameInfo = JsonSerializer.Deserialize<GameInfo>(content);

                if (gameInfo?.Type == null)
                {
                    Logger.Debug("Game info returned null type");
                    return null;
                }

                var detectedType = ParseGameType(gameInfo.Type);
                Logger.Debug("Detected game type: {RawType} -> {Detected}", gameInfo.Type, detectedType);

                return detectedType;
            }
            catch (HttpRequestException ex)
            {
                Logger.Warning(ex, "Connection failed (SC2 service unavailable)");
                return null;
            }
            catch (TimeoutException)
            {
                Logger.Warning("Timeout querying SC2 game service");
                return null;
            }
            catch (JsonException ex)
            {
                Logger.Error(ex, "Failed to parse game info JSON");
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error while detecting game type");
                return null;
            }
        }

        /// <summary>
        /// Parse game type string from SC2 service into GameType enum.
        /// </summary>
        private static GameType? ParseGameType(string typeString)
        {
            return typeString switch
            {
                "1v1" => GameType.Solo1v1,
                "2v2" => GameType.Team2v2,
                "3v3" => GameType.Team3v3,
                "4v4" => GameType.Team4v4,
                _ => null
            };
        }

        /// <summary>
        /// Represents JSON response from SC2 localhost game service.
        /// </summary>
        private class GameInfo
        {
            public string? Type { get; set; }
            public bool IsReplay { get; set; }
            public string? Map { get; set; }
        }
    }

    /// <summary>
    /// Enumeration of supported StarCraft II game types.
    /// </summary>
    public enum GameType
    {
        Solo1v1,
        Team2v2,
        Team3v3,
        Team4v4
    }
}
