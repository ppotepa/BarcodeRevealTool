using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Presentation;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace BarcodeRevealTool.Persistence.Replay
{
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
                
                // Mark cache as complete
                File.WriteAllText(CacheLockFile, DateTime.UtcNow.ToString("O"));
                _logger.Information("Cache initialization complete");
            }
        }

        /// <summary>
        /// Synchronize missing replays from disk (incremental update).
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

            var missingFiles = _queryService.GetMissingReplayFiles(allReplayFiles);
            _logger.Information("Found {MissingCount} missing replay files", missingFiles.Count);

            if (missingFiles.Count > 0)
            {
                await ProcessReplaysAsync(missingFiles.ToArray());
            }
        }

        private async Task ProcessReplaysAsync(string[] replayFiles)
        {
            int maxParallelism = GetOptimalDegreeOfParallelism();
            
            _logger.Information("Processing {ReplayCount} replays with {Parallelism} parallel threads",
                replayFiles.Length, maxParallelism);

            // Stage 1: Load metadata sequentially
            var metadataList = await ReplayMetadataExtractor.ExtractMetadataFromFilesAsync(
                replayFiles,
                maxParallelism);

            _logger.Information("Extracted metadata for {MetadataCount} replays", metadataList.Count);

            // Stage 2: Insert into database in parallel
            var semaphore = new System.Threading.SemaphoreSlim(maxParallelism);
            var tasks = new List<Task>();
            int processedCount = 0;
            var lockObj = new object();

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
                            if (processedCount % 10 == 0)
                            {
                                _logger.Debug("Processed {ProcessedCount}/{TotalCount} replays",
                                    processedCount, metadataList.Count);
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

            _logger.Information("Cache processing complete");
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
