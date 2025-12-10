using BarcodeRevealTool.Engine.Config;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Callback for cache operation progress.
    /// </summary>
    public delegate void CacheProgressCallback(string phase, int current, int total, string? message = null);

    /// <summary>
    /// Service for caching StarCraft II replay files.
    /// Handles scanning replay folders, extracting metadata, and storing in database.
    /// </summary>
    public class ReplayCacheService
    {
        private readonly IConfiguration _configuration;
        private readonly ReplayQueryService _queryService;
        private readonly ILogger _logger = Log.ForContext<ReplayCacheService>();
        private string CacheLockFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");

        public event CacheProgressCallback? OnProgress;

        public ReplayCacheService(IConfiguration configuration, ReplayQueryService queryService)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        }

        /// <summary>
        /// Initialize cache by scanning all replay files from configured folder.
        /// </summary>
        public async Task InitializeCacheAsync()
        {
            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                _logger.Warning("Replay folder not configured or doesn't exist");
                return;
            }

            _logger.Information("Initializing cache from replay folder: {ReplayFolder}", appSettings.Replays.Folder);

            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var allReplayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            _logger.Information("Found {ReplayCount} replay files on disk", allReplayFiles.Length);

            if (allReplayFiles.Length > 0)
            {
                await ProcessReplaysAsync(allReplayFiles);
                _logger.Information("Cache initialization complete");
            }
        }

        /// <summary>
        /// Synchronize missing replays from disk (incremental update).
        /// Only adds replays newer than the most recent replay in the cache.
        /// </summary>
        public async Task SyncMissingReplaysAsync()
        {
            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                _logger.Warning("Replay folder not configured or doesn't exist");
                return;
            }

            _logger.Information("Syncing missing replays from: {ReplayFolder}", appSettings.Replays.Folder);

            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var allReplayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            // Get the most recent replay date from the cache
            // Only add replays newer than this date
            var mostRecentCachedDate = _queryService.GetMostRecentReplayDate();

            List<string> newReplayFiles;
            if (mostRecentCachedDate.HasValue)
            {
                // Filter to only replays newer than the most recent cached replay
                newReplayFiles = allReplayFiles
                    .Where(f =>
                    {
                        var fileInfo = new FileInfo(f);
                        // Add if file is newer than cached date
                        return fileInfo.LastWriteTimeUtc > mostRecentCachedDate.Value;
                    })
                    .ToList();
                _logger.Information("Found {NewCount} replays newer than {MostRecentDate}",
                    newReplayFiles.Count, mostRecentCachedDate.Value);
            }
            else
            {
                // No cached replays, add all files on disk
                newReplayFiles = allReplayFiles.ToList();
                _logger.Information("No cached replays found. Will add {TotalCount} files", allReplayFiles.Length);
            }

            if (newReplayFiles.Count > 0)
            {
                await ProcessReplaysAsync(newReplayFiles.ToArray());
            }
        }

        /// <summary>
        /// Synchronize only the most recently modified replay file.
        /// Used when a game has just finished to add only that replay.
        /// </summary>
        public async Task SyncRecentReplayAsync()
        {
            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                _logger.Warning("Replay folder not configured or doesn't exist");
                return;
            }

            _logger.Information("Syncing most recent replay from: {ReplayFolder}", appSettings.Replays.Folder);

            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var allReplayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            if (allReplayFiles.Length == 0)
            {
                _logger.Information("No replay files found");
                return;
            }

            // Get the most recently modified replay file
            var mostRecentReplay = allReplayFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();

            if (mostRecentReplay == null)
            {
                _logger.Warning("Could not determine most recent replay file");
                return;
            }

            // Check if this replay is already in the cache
            if (_queryService.IsReplayInCache(mostRecentReplay.FullName))
            {
                _logger.Information("Most recent replay already cached: {ReplayFile}", mostRecentReplay.Name);
                return;
            }

            _logger.Information("Found new replay to add: {ReplayFile} (modified {LastWriteTime})",
                mostRecentReplay.Name, mostRecentReplay.LastWriteTimeUtc);

            await ProcessReplaysAsync(new[] { mostRecentReplay.FullName });
        }

        private async Task ProcessReplaysAsync(string[] replayFiles)
        {
            int maxParallelism = GetOptimalDegreeOfParallelism();

            _logger.Information("════════════════════════════════════════════════════════════════");
            _logger.Information("Caching {ReplayCount} Replays ({ParallelismCount} Parallel Threads)",
                replayFiles.Length, maxParallelism);
            _logger.Information("════════════════════════════════════════════════════════════════");

            _logger.Information("Processing {ReplayCount} replays with {Parallelism} parallel threads",
                replayFiles.Length, maxParallelism);

            // Stage 1: Load metadata sequentially
            _logger.Information("Phase 1: Extracting Metadata from Replays");

            var metadataList = await ReplayMetadataExtractor.ExtractMetadataFromFilesAsync(
                replayFiles,
                maxParallelism,
                (current, total) =>
                {
                    OnProgress?.Invoke("Extracting", current, total, $"Processing {current}/{total}");
                    DisplayProgress("Metadata Extraction", current, total, "files");
                });

            _logger.Information("Extracted metadata for {MetadataCount} replays", metadataList.Count);

            // Stage 2: Insert into database in parallel
            _logger.Information("Phase 2: Storing Replays in Database");

            var semaphore = new System.Threading.SemaphoreSlim(maxParallelism);
            var tasks = new List<Task>();
            int processedCount = 0;
            var lockObj = new object();
            var startTime = DateTime.UtcNow;

            foreach (var (filePath, metadata) in metadataList)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        if (metadata != null)
                        {
                            _queryService.AddOrUpdateReplay(metadata);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to process replay: {FilePath}", filePath);
                    }
                    finally
                    {
                        lock (lockObj)
                        {
                            processedCount++;
                            OnProgress?.Invoke("Storing", processedCount, metadataList.Count,
                                $"Stored {processedCount}/{metadataList.Count}");

                            if (processedCount % 50 == 0 || processedCount == metadataList.Count)
                            {
                                var elapsed = DateTime.UtcNow - startTime;
                                var replayPerSecond = processedCount > 0 ? processedCount / elapsed.TotalSeconds : 0;
                                var remaining = metadataList.Count - processedCount;
                                var eta = replayPerSecond > 0 ?
                                    TimeSpan.FromSeconds(remaining / replayPerSecond) :
                                    TimeSpan.Zero;

                                DisplayProgress("Database Storage", processedCount, metadataList.Count, "replays",
                                    $"{replayPerSecond:F1} replays/sec | ETA: {eta.Hours:D2}:{eta.Minutes:D2}:{eta.Seconds:D2}");
                            }
                        }
                        semaphore.Release();
                    }
                }));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            _logger.Information("✓ Cache processing complete - {ProcessedCount} replays stored", processedCount);
        }

        /// <summary>
        /// Log progress information to Serilog.
        /// Periodically logs progress at intervals to avoid log spam.
        /// </summary>
        private void DisplayProgress(string phase, int current, int total, string unit, string? extraInfo = null)
        {
            if (total <= 0)
                return;

            double percentage = (current / (double)total) * 100;

            // Log at specific milestones: 0%, 25%, 50%, 75%, 100%, and every 50 items
            bool isMilestone = percentage == 0 || percentage >= 100 ||
                              (percentage % 25 < (1.0 / total * 100)) ||
                              current % 50 == 0;

            if (isMilestone)
            {
                if (string.IsNullOrEmpty(extraInfo))
                {
                    _logger.Information("{Phase}: {Percentage:F1}% ({Current}/{Total} {Unit})",
                        phase, percentage, current, total, unit);
                }
                else
                {
                    _logger.Information("{Phase}: {Percentage:F1}% ({Current}/{Total} {Unit}) | {ExtraInfo}",
                        phase, percentage, current, total, unit, extraInfo);
                }
            }
        }

        private int GetOptimalDegreeOfParallelism()
        {
            int processorCount = Environment.ProcessorCount;
            int optimalDegree;

            if (processorCount <= 2)
                optimalDegree = 1;
            else if (processorCount <= 4)
                optimalDegree = Math.Max(1, processorCount / 2);
            else if (processorCount <= 8)
                optimalDegree = Math.Max(1, (processorCount * 3) / 5);
            else if (processorCount <= 16)
                optimalDegree = Math.Max(1, (processorCount * 7) / 10);
            else
                optimalDegree = Math.Max(1, (processorCount * 3) / 4);

            _logger.Debug("System Info - Processors: {ProcessorCount}, Optimal Parallelism: {OptimalDegree}",
                processorCount, optimalDegree);

            return optimalDegree;
        }
    }
}
