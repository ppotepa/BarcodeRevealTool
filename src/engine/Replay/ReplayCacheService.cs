using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Replay;
using Microsoft.Extensions.Configuration;

namespace BarcodeRevealTool.Engine.Replay
{
    /// <summary>
    /// Handles replay file caching operations:
    /// - Scanning replay folders
    /// - Extracting metadata
    /// - Managing cache lifecycle (validation, invalidation)
    /// - Parallel processing of replay files
    /// </summary>
    public interface IReplayCacheService
    {
        /// <summary>
        /// Initialize complete cache on first run or refresh all replays.
        /// Scans folder recursively (if configured), extracts metadata, stores in database.
        /// </summary>
        Task InitializeFullCacheAsync(IReplayQueryService queryService, IOutputProvider outputProvider);

        /// <summary>
        /// Synchronize missing replays from disk.
        /// Finds replay files not yet in database and adds them.
        /// Called during game transitions to find opponent history.
        /// </summary>
        Task SyncMissingReplaysAsync(IReplayQueryService queryService, IOutputProvider outputProvider);

        /// <summary>
        /// Check if cache validation interval has expired.
        /// Returns true if should rescan for changes (every 60 minutes).
        /// </summary>
        bool ShouldRescanForChanges();

        /// <summary>
        /// Update cache validation timestamp.
        /// Called after cache operations to prevent unnecessary re-scans.
        /// </summary>
        void UpdateCacheValidationTime();
    }

    public class ReplayCacheService : IReplayCacheService
    {
        private readonly IConfiguration _configuration;
        private string CacheLockFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
        private string CacheValidationFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.validation");
        private const int CacheValidationIntervalMinutes = 60;

        public ReplayCacheService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Calculate optimal degree of parallelism based on system resources.
        /// Uses CPU core count, but reserves cores for system/UI responsiveness.
        /// </summary>
        private int GetOptimalDegreeOfParallelism()
        {
            int processorCount = Environment.ProcessorCount;
            int optimalDegree;

            if (processorCount <= 2)
            {
                // Single or dual core: use 1 thread (don't overwhelm)
                optimalDegree = 1;
            }
            else if (processorCount <= 4)
            {
                // 2-4 cores: use half (1-2 threads)
                optimalDegree = Math.Max(1, processorCount / 2);
            }
            else if (processorCount <= 8)
            {
                // 4-8 cores: use 60% (better throughput)
                optimalDegree = Math.Max(1, (processorCount * 3) / 5);
            }
            else if (processorCount <= 16)
            {
                // 8-16 cores: use 70% (aggressive but still reserves resources)
                optimalDegree = Math.Max(1, (processorCount * 7) / 10);
            }
            else
            {
                // 16+ cores: use 75% (high parallelism)
                optimalDegree = Math.Max(1, (processorCount * 3) / 4);
            }

            System.Diagnostics.Debug.WriteLine(
                $"[ReplayCacheService] System Info - Processors: {processorCount}, " +
                $"Optimal Parallelism: {optimalDegree}, " +
                $"Utilization: {(optimalDegree * 100) / processorCount}%"
            );

            return optimalDegree;
        }

        public async Task InitializeFullCacheAsync(IReplayQueryService queryService, IOutputProvider outputProvider)
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] InitializeFullCacheAsync started");

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Replays folder not configured or doesn't exist");
                return;
            }

            outputProvider.RenderCacheInitializingMessage();

            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var allReplayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Found {allReplayFiles.Length} replay files on disk");

            if (allReplayFiles.Length > 0)
            {
                await ProcessReplaysAsync(queryService, allReplayFiles, allReplayFiles.Length, outputProvider);
                outputProvider.RenderCacheComplete();
            }

            // Mark cache as built
            File.WriteAllText(CacheLockFile, DateTime.UtcNow.ToString("O"));
            UpdateCacheValidationTime();
            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Full cache initialized, validation time updated");
        }

        public async Task SyncMissingReplaysAsync(IReplayQueryService queryService, IOutputProvider outputProvider)
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] SyncMissingReplaysAsync started");

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Replays folder not configured or doesn't exist");
                return;
            }

            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var allReplayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Found {allReplayFiles.Length} replay files on disk");

            // Get missing files (on disk but not in cache)
            var missingFiles = queryService.GetMissingReplayFiles(allReplayFiles);
            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Found {missingFiles.Count} missing replay files");

            if (missingFiles.Count > 0)
            {
                outputProvider.RenderCacheSyncMessage();
                await ProcessReplaysAsync(queryService, missingFiles.ToArray(), missingFiles.Count, outputProvider);
                outputProvider.RenderCacheComplete();
            }

            // Update validation time even if nothing was synced
            UpdateCacheValidationTime();
            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Sync complete, validation time updated");
        }

        public bool ShouldRescanForChanges()
        {
            if (!File.Exists(CacheValidationFile))
            {
                return true; // First time - should rescan
            }

            try
            {
                var validationContent = File.ReadAllText(CacheValidationFile).Trim();
                if (DateTime.TryParse(validationContent, out var lastValidationTime))
                {
                    var timeSinceValidation = DateTime.UtcNow - lastValidationTime;
                    bool shouldRescan = timeSinceValidation.TotalMinutes >= CacheValidationIntervalMinutes;
                    System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Last validation: {lastValidationTime}, Elapsed: {timeSinceValidation.TotalMinutes:F1} min, Should rescan: {shouldRescan}");
                    return shouldRescan;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Error reading validation file: {ex.Message}");
            }

            return true; // Default to rescan on error
        }

        public void UpdateCacheValidationTime()
        {
            try
            {
                File.WriteAllText(CacheValidationFile, DateTime.UtcNow.ToString("O"));
                System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Cache validation time updated to {DateTime.UtcNow:O}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Error updating validation time: {ex.Message}");
            }
        }

        /// <summary>
        /// Process replays in parallel and add metadata to database.
        /// Loads all replay metadata into memory first (Stage 1), then processes in parallel (Stage 2).
        /// </summary>
        private async Task ProcessReplaysAsync(
            IReplayQueryService queryService,
            string[] replayFiles,
            int totalCount,
            IOutputProvider outputProvider)
        {
            int maxDegreeOfParallelism = GetOptimalDegreeOfParallelism();

            System.Diagnostics.Debug.WriteLine(
                $"[ReplayCacheService] Starting cache build with {replayFiles.Length} files, " +
                $"{maxDegreeOfParallelism} parallel threads"
            );

            // Stage 1: Load all replay metadata into memory (fast sequential scan)
            var replayMetadataList = await LoadReplayMetadataAsync(replayFiles);

            System.Diagnostics.Debug.WriteLine(
                $"[ReplayCacheService] Loaded {replayMetadataList.Count} replay metadata into memory"
            );

            // Stage 2: Process loaded metadata in parallel for database insertion
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();
            int processedCount = 0;
            var lockObj = new object();

            foreach (var (filePath, metadata) in replayMetadataList)
            {
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        if (metadata != null)
                        {
                            // Store metadata in database
                            queryService.AddOrUpdateReplay(
                                metadata.Players.Count > 0 ? metadata.Players[0].Name : string.Empty,
                                metadata.Players.Count > 1 ? metadata.Players[1].Name : string.Empty,
                                !string.IsNullOrEmpty(metadata.Map) ? metadata.Map : "Unknown",
                                metadata.Players.Count > 0 ? metadata.Players[0].Race : string.Empty,
                                metadata.Players.Count > 1 ? metadata.Players[1].Race : string.Empty,
                                metadata.GameDate,
                                metadata.FilePath,
                                metadata.SC2ClientVersion,
                                metadata.Players.Count > 0 ? metadata.Players[0].PlayerId : null,
                                metadata.Players.Count > 1 ? metadata.Players[1].PlayerId : null
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Error processing replay: {ex}");
                        outputProvider.RenderWarning($"Failed to cache {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                    finally
                    {
                        lock (lockObj)
                        {
                            processedCount++;
                            outputProvider.RenderCacheProgress(processedCount, totalCount);
                        }
                        semaphore.Release();
                    }
                }));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayCacheService] Cache processing complete");
        }

        /// <summary>
        /// Load replay metadata into memory (Stage 1).
        /// Sequentially reads all replay files and extracts metadata.
        /// This is fast and done before parallel processing for better resource management.
        /// </summary>
        private async Task<List<(string filePath, ReplayMetadata? metadata)>> LoadReplayMetadataAsync(string[] replayFiles)
        {
            var metadataList = new List<(string, ReplayMetadata?)>();
            int loadedCount = 0;

            await Task.Run(() =>
            {
                foreach (var replayFile in replayFiles)
                {
                    try
                    {
                        // Extract metadata only (fast - no full decode)
                        var metadata = BuildOrderReader.GetReplayMetadataFast(replayFile);
                        metadataList.Add((replayFile, metadata));
                        loadedCount++;

                        // Log progress every 100 files
                        if (loadedCount % 100 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[ReplayCacheService] Loaded {loadedCount}/{replayFiles.Length} replay metadata into memory"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ReplayCacheService] Error loading metadata for {Path.GetFileName(replayFile)}: {ex.Message}"
                        );
                        metadataList.Add((replayFile, null));
                    }
                }
            });

            return metadataList;
        }
    }
}
