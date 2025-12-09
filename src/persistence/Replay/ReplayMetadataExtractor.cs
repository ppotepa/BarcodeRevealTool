using System;
using System.Threading.Tasks;
using Serilog;

namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Extracts metadata from StarCraft II replay files.
    /// Uses a simple file-based approach for fast metadata extraction.
    /// </summary>
    public static class ReplayMetadataExtractor
    {
        private static readonly ILogger _logger = Log.ForContext(typeof(ReplayMetadataExtractor));

        /// <summary>
        /// Extract basic metadata from a replay file.
        /// This performs fast extraction without full replay decoding.
        /// </summary>
        public static async Task<ReplayMetadata?> ExtractMetadataAsync(string replayFilePath)
        {
            try
            {
                if (!File.Exists(replayFilePath))
                {
                    _logger.Warning("Replay file not found: {FilePath}", replayFilePath);
                    return null;
                }

                var fileInfo = new FileInfo(replayFilePath);
                var fileName = fileInfo.Name;
                var gameDate = fileInfo.LastWriteTimeUtc;

                // Create basic metadata from file information
                // In a full implementation, this would parse the replay file to extract:
                // - Player names
                // - Battle tags
                // - Map name
                // - Race selections
                // - Actual game date

                var metadata = new ReplayMetadata
                {
                    ReplayFilePath = replayFilePath,
                    ReplayGuid = ReplayMetadata.ComputeDeterministicGuid(fileName, gameDate),
                    GameDate = gameDate,
                    Map = "Unknown",
                    YourPlayer = "Unknown",
                    OpponentPlayer = "Unknown",
                    YourRace = "Unknown",
                    OpponentRace = "Unknown",
                    SC2ClientVersion = null
                };

                _logger.Debug("Extracted metadata for replay: {FileName}", fileName);
                return await Task.FromResult(metadata);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to extract metadata from replay: {FilePath}", replayFilePath);
                return null;
            }
        }

        /// <summary>
        /// Extract metadata from multiple replay files in parallel.
        /// </summary>
        public static async Task<List<(string filePath, ReplayMetadata? metadata)>> ExtractMetadataFromFilesAsync(
            string[] replayFiles,
            int maxDegreeOfParallelism = 4)
        {
            var results = new List<(string, ReplayMetadata?)>();
            var semaphore = new System.Threading.SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();

            foreach (var replayFile in replayFiles)
            {
                await semaphore.WaitAsync();
                
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var metadata = await ExtractMetadataAsync(replayFile);
                        lock (results)
                        {
                            results.Add((replayFile, metadata));
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return results;
        }
    }
}
