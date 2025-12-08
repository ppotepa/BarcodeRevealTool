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

        public Action? OnCacheOperationComplete { get; set; }

        public ReplayService(IOutputProvider outputProvider, IConfiguration configuration)
        {
            _outputProvider = outputProvider;
            _configuration = configuration;
        }

        public async Task InitializeCacheAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayService] InitializeCacheAsync started");
            // Check if cache has already been built
            if (File.Exists(CacheLockFile))
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache lock file exists, cache is already initialized");
                BuildOrderReader.InitializeCache();
                await SyncReplaysFromDiskAsync();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayService] No cache lock file found, initializing cache");
            _outputProvider.RenderCacheInitializingMessage();

            // Initialize the cache/database (creates replays.db if needed)
            BuildOrderReader.InitializeCache();

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            // On first startup: Build complete cache from all replays in folder
            if (appSettings?.Replays?.Folder != null && Directory.Exists(appSettings.Replays.Folder))
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Scanning replays folder: {appSettings.Replays.Folder}");
                var searchOption = (appSettings.Replays.Recursive == true)
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var replayFiles = Directory.GetFiles(
                    appSettings.Replays.Folder,
                    "*.SC2Replay",
                    searchOption);

                System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {replayFiles.Length} replay files");
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
                                System.Diagnostics.Debug.WriteLine($"[ReplayService] Processing replay: {Path.GetFileName(replayFile)}");
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
                                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error processing replay: {ex}");
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
            System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache initialization complete, lock file created");

            // Notify that cache operation is complete so UI can refresh
            OnCacheOperationComplete?.Invoke();
        }

        public async Task SyncReplaysFromDiskAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayService] SyncReplaysFromDiskAsync started");
            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Replays folder not configured");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayService] Scanning folder: {appSettings.Replays.Folder}");
            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var replayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {replayFiles.Length} replay files");
            var database = BuildOrderReader.GetDatabase();
            if (database == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Database is null");
                return;
            }

            // Show sync progress message
            if (replayFiles.Length > 0)
            {
                _outputProvider.RenderCacheSyncMessage();
            }

            // Use parallel processing with bounded concurrency (4 concurrent decoders)
            int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();
            int processedCount = 0;
            int newReplaysAdded = 0;
            var lockObj = new object();

            // Check each replay file in parallel
            // IMPORTANT: Check database FIRST before expensive file decode
            foreach (var replayFile in replayFiles)
            {
                // Skip if already cached (fast DB check before decode)
                if (database.GetReplayByFilePath(replayFile) != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Skipping cached replay: {Path.GetFileName(replayFile)}");
                    lock (lockObj)
                    {
                        processedCount++;
                        _outputProvider.RenderCacheProgress(processedCount, replayFiles.Length);
                    }
                    continue;
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayService] Processing new replay: {Path.GetFileName(replayFile)}");
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
                            System.Diagnostics.Debug.WriteLine($"[ReplayService] Replay added to cache: {Path.GetFileName(replayFile)}");
                            lock (lockObj)
                            {
                                newReplaysAdded++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReplayService] Error syncing replay: {ex}");
                        _outputProvider.RenderWarning($"Failed to sync {Path.GetFileName(replayFile)}: {ex.Message}");
                    }
                    finally
                    {
                        lock (lockObj)
                        {
                            processedCount++;
                            _outputProvider.RenderCacheProgress(processedCount, replayFiles.Length);
                        }
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

            // Notify that cache operation is complete so UI can refresh
            OnCacheOperationComplete?.Invoke();
        }

        /// <summary>
        /// Save a single replay to the database without scanning the folder.
        /// Called when exiting a game to save the replay that just finished playing.
        /// </summary>
        public async Task SaveReplayToDbAsync(string replayFilePath)
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayService] SaveReplayToDbAsync: {Path.GetFileName(replayFilePath)}");
            try
            {
                if (!File.Exists(replayFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Replay file not found: {replayFilePath}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayService] File exists, checking database");
                var database = BuildOrderReader.GetDatabase();
                if (database == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Database is null");
                    return;
                }

                // Check if already in database
                if (database.GetReplayByFilePath(replayFilePath) != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Replay already in database");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayService] Decoding new replay");
                // Decode and save this single replay
                var metadata = BuildOrderReader.GetReplayMetadataFast(replayFilePath);
                if (metadata != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Saving metadata to database");
                    database.CacheMetadata(metadata);
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Replay saved successfully");
                    _outputProvider.RenderWarning($"[+] Saved replay: {Path.GetFileName(replayFilePath)}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Failed to decode replay metadata");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Exception in SaveReplayToDbAsync: {ex}");
                _outputProvider.RenderWarning($"Failed to save replay: {ex.Message}");
            }
        }
    }
}
