using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Replay;
using Microsoft.Extensions.Configuration;

namespace BarcodeRevealTool.Services
{
    /// <summary>
    /// Concrete implementation of IReplayService using BuildOrderReader and ReplayDatabase
    /// </summary>
    public class ReplayService : IReplayService
    {
        private readonly IOutputProvider _outputProvider;
        private readonly IConfiguration _configuration;
        private string CacheLockFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");

        public ReplayService(IOutputProvider outputProvider, IConfiguration configuration)
        {
            _outputProvider = outputProvider;
            _configuration = configuration;
        }

        public async Task InitializeCacheAsync()
        {
            // Check if cache has already been built
            if (File.Exists(CacheLockFile))
            {
                _outputProvider.RenderCacheSyncMessage();
                BuildOrderReader.InitializeCache();
                await SyncReplaysFromDiskAsync();
                return;
            }

            _outputProvider.RenderCacheInitializingMessage();

            // Initialize the cache/database (creates replays.db if needed)
            BuildOrderReader.InitializeCache();

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            // On first startup: Build complete cache from all replays in folder
            if (appSettings?.Replays?.Folder != null && Directory.Exists(appSettings.Replays.Folder))
            {
                var searchOption = (appSettings.Replays.Recursive == true)
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var replayFiles = Directory.GetFiles(
                    appSettings.Replays.Folder,
                    "*.SC2Replay",
                    searchOption);

                if (replayFiles.Length > 0)
                {
                    // Use parallel processing with bounded concurrency (4 concurrent decoders)
                    int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
                    var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
                    var database = BuildOrderReader.GetDatabase();

                    var tasks = new List<Task>();
                    int processedCount = 0;
                    var lockObj = new object();

                    // Scan ALL replays to build complete cache on first startup
                    // (no database checks - we're building from scratch)
                    foreach (var replayFile in replayFiles)
                    {
                        await semaphore.WaitAsync();
                        tasks.Add(Task.Run(async () =>
                        {
                            try
                            {
                                var metadata = BuildOrderReader.GetReplayMetadataFast(replayFile);

                                if (metadata != null && database != null)
                                {
                                    database.CacheMetadata(metadata);
                                }

                                lock (lockObj)
                                {
                                    processedCount++;
                                    _outputProvider.RenderCacheProgress(processedCount, replayFiles.Length);
                                }
                            }
                            catch (Exception ex)
                            {
                                _outputProvider.RenderWarning($"Failed to cache {Path.GetFileName(replayFile)}: {ex.Message}");
                            }
                            finally
                            {
                                semaphore.Release();
                            }
                        }));
                    }

                    // Wait for all tasks to complete
                    await Task.WhenAll(tasks);
                    _outputProvider.RenderCacheComplete();
                }
            }

            // Create cache lock file to prevent re-scanning on future startups
            // This ensures full cache is only built once on first startup
            File.WriteAllText(CacheLockFile, DateTime.UtcNow.ToString("O"));
        }

        public async Task SyncReplaysFromDiskAsync()
        {
            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null)
                return;

            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var replayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            var database = BuildOrderReader.GetDatabase();
            if (database == null)
                return;

            // Use parallel processing with bounded concurrency (4 concurrent decoders)
            int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();
            int newReplaysAdded = 0;
            var lockObj = new object();

            // Check each replay file in parallel
            // IMPORTANT: Check database FIRST before expensive file decode
            foreach (var replayFile in replayFiles)
            {
                // Skip if already cached (fast DB check before decode)
                if (database.GetReplayByFilePath(replayFile) != null)
                    continue;

                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        // Only decode if NOT already in database
                        var metadata = BuildOrderReader.GetReplayMetadataFast(replayFile);
                        if (metadata != null)
                        {
                            database.CacheMetadata(metadata);
                            lock (lockObj)
                            {
                                newReplaysAdded++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _outputProvider.RenderWarning($"Failed to sync {Path.GetFileName(replayFile)}: {ex.Message}");
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            if (newReplaysAdded > 0)
            {
                _outputProvider.RenderSyncComplete(newReplaysAdded);
            }
        }

        /// <summary>
        /// Save a single replay to the database without scanning the folder.
        /// Called when exiting a game to save the replay that just finished playing.
        /// </summary>
        public async Task SaveReplayToDbAsync(string replayFilePath)
        {
            try
            {
                if (!File.Exists(replayFilePath))
                    return;

                var database = BuildOrderReader.GetDatabase();
                if (database == null)
                    return;

                // Check if already in database
                if (database.GetReplayByFilePath(replayFilePath) != null)
                    return;

                // Decode and save this single replay
                var metadata = BuildOrderReader.GetReplayMetadataFast(replayFilePath);
                if (metadata != null)
                {
                    database.CacheMetadata(metadata);
                    _outputProvider.RenderWarning($"✓ Saved replay: {Path.GetFileName(replayFilePath)}");
                }
            }
            catch (Exception ex)
            {
                _outputProvider.RenderWarning($"Failed to save replay: {ex.Message}");
            }
        }
    }
}
