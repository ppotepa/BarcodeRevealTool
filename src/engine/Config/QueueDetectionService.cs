using System.Net.Http.Json;
using System.Text.Json;
using Sc2Pulse.Models;

namespace BarcodeRevealTool.Engine.Config
{
    /// <summary>
    /// Detects the queue type (1v1, 2v2, 3v3, 4v4, etc.) by querying the local SC2 API.
    /// The SC2 game client exposes a local HTTP API on localhost:6119 during gameplay.
    /// </summary>
    public class QueueDetectionService
    {
        private static readonly string SC2_API_ENDPOINT = "http://127.0.0.1:6119/game";
        private static readonly HttpClient _httpClient = new();

        /// <summary>
        /// Detect the queue type by querying the SC2 game API and analyzing player count.
        /// Returns null if unable to detect.
        /// </summary>
        public static async Task<Queue?> DetectQueueTypeAsync(int timeoutSeconds = 5)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

                System.Diagnostics.Debug.WriteLine($"[QueueDetection] Querying SC2 API at {SC2_API_ENDPOINT}");

                var response = await _httpClient.GetAsync(SC2_API_ENDPOINT, cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    System.Diagnostics.Debug.WriteLine($"[QueueDetection] SC2 API returned {response.StatusCode}");
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[QueueDetection] SC2 API response: {content.Substring(0, Math.Min(200, content.Length))}...");

                // Parse the JSON response to get player information
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                // Try to get the number of players from the game state
                // The response typically contains game info with player list
                if (root.TryGetProperty("players", out var playersElement))
                {
                    if (playersElement.ValueKind == JsonValueKind.Array)
                    {
                        int playerCount = playersElement.GetArrayLength();
                        System.Diagnostics.Debug.WriteLine($"[QueueDetection] Detected {playerCount} players in game");

                        // Determine queue type based on player count
                        Queue? queueType = playerCount switch
                        {
                            1 => Queue.LOTV_1V1,
                            2 => Queue.LOTV_1V1,  // 1v1 has 2 players total (you + opponent)
                            4 => Queue.LOTV_2V2,  // 2v2 has 4 players
                            6 => Queue.LOTV_3V3,  // 3v3 has 6 players
                            8 => Queue.LOTV_4V4,  // 4v4 has 8 players
                            _ => null
                        };

                        if (queueType.HasValue)
                        {
                            System.Diagnostics.Debug.WriteLine($"[QueueDetection] Determined queue type: {queueType.Value}");
                            return queueType;
                        }
                    }
                }

                // Fallback: try to get player count from alternative location in JSON
                if (root.TryGetProperty("gameState", out var gameStateElement))
                {
                    if (gameStateElement.TryGetProperty("players", out var gsPlayersElement))
                    {
                        if (gsPlayersElement.ValueKind == JsonValueKind.Array)
                        {
                            int playerCount = gsPlayersElement.GetArrayLength();
                            System.Diagnostics.Debug.WriteLine($"[QueueDetection] Detected {playerCount} players from gameState");

                            Queue? queueType = playerCount switch
                            {
                                2 => Queue.LOTV_1V1,
                                4 => Queue.LOTV_2V2,
                                6 => Queue.LOTV_3V3,
                                8 => Queue.LOTV_4V4,
                                _ => null
                            };

                            if (queueType.HasValue)
                            {
                                System.Diagnostics.Debug.WriteLine($"[QueueDetection] Determined queue type from gameState: {queueType.Value}");
                                return queueType;
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[QueueDetection] Could not determine queue type from API response");
                return null;
            }
            catch (System.Threading.Tasks.TaskCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[QueueDetection] SC2 API request timed out after {timeoutSeconds} seconds");
                return null;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QueueDetection] HTTP error: {ex.Message}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[QueueDetection] Error detecting queue type: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Convert queue type to player count.
        /// </summary>
        public static int GetPlayerCountFromQueue(Queue queueType)
        {
            return queueType switch
            {
                Queue.LOTV_1V1 or Queue.HOTS_1V1 or Queue.WOL_1V1 => 2,
                Queue.LOTV_2V2 or Queue.HOTS_2V2 or Queue.WOL_2V2 => 4,
                Queue.LOTV_3V3 or Queue.HOTS_3V3 or Queue.WOL_3V3 => 6,
                Queue.LOTV_4V4 or Queue.HOTS_4V4 or Queue.WOL_4V4 => 8,
                Queue.LOTV_ARCHON => 2,  // Archon is 1v1 with 2 players per team
                _ => 2  // Default to 1v1
            };
        }

        /// <summary>
        /// Get the team size (players per team) from queue type.
        /// </summary>
        public static int GetTeamSizeFromQueue(Queue queueType)
        {
            return queueType switch
            {
                Queue.LOTV_1V1 or Queue.HOTS_1V1 or Queue.WOL_1V1 => 1,
                Queue.LOTV_2V2 or Queue.HOTS_2V2 or Queue.WOL_2V2 => 2,
                Queue.LOTV_3V3 or Queue.HOTS_3V3 or Queue.WOL_3V3 => 3,
                Queue.LOTV_4V4 or Queue.HOTS_4V4 or Queue.WOL_4V4 => 4,
                Queue.LOTV_ARCHON => 2,  // Archon is 2v2
                _ => 1  // Default to solo
            };
        }
    }
}
