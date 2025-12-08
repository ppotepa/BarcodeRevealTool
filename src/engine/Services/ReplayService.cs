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
        private CacheManager _cacheManager = new();

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
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache lock file exists, loading cached replays into memory");
                BuildOrderReader.InitializeCache();

                // Load all cached replays into memory for efficient sync
                var database = BuildOrderReader.GetDatabase();
                if (database != null)
                {
                    _cacheManager.LoadCachedFilePaths(database);
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Loaded {_cacheManager.CachedFileCount} cached replays into memory");
                }

                // Only sync if user has added new replays to the folder
                // Skip sync if cache is up to date
                await SyncReplaysFromDiskAsync();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayService] No cache lock file found, initializing cache from scratch");
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
                    // No database checks - we're building from scratch into empty database
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
                                    // Direct insert - skip CacheMetadata's GetReplayByFilePath check
                                    // since database is empty on first run
                                    database.AddOrUpdateReplay(
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

                                    // Add to in-memory cache
                                    _cacheManager.AddCachedFile(metadata.FilePath);
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
                OnCacheOperationComplete?.Invoke();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayService] Scanning folder: {appSettings.Replays.Folder}");
            var searchOption = (appSettings.Replays.Recursive == true)
                ? SearchOption.AllDirectories
                : SearchOption.TopDirectoryOnly;

            var allReplayFiles = Directory.GetFiles(
                appSettings.Replays.Folder,
                "*.SC2Replay",
                searchOption);

            System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {allReplayFiles.Length} replay files on disk");
            var database = BuildOrderReader.GetDatabase();
            if (database == null)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Database is null");
                OnCacheOperationComplete?.Invoke();
                return;
            }

            // Get list of NEW replay files (on disk but not in cache)
            var newReplayFiles = _cacheManager.GetNewReplayFiles(allReplayFiles);

            System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {newReplayFiles.Count} new replay files to add");

            if (newReplayFiles.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache is up to date, no new replays to process");
                OnCacheOperationComplete?.Invoke();
                return;
            }

            // Show sync progress message only if there are new files
            _outputProvider.RenderCacheSyncMessage();

            // Use parallel processing with bounded concurrency
            int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
            var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();
            int processedCount = 0;
            int newReplaysAdded = 0;
            var lockObj = new object();

            // Process only NEW replay files (skip all cached ones)
            foreach (var replayFile in newReplayFiles)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Queuing new replay: {Path.GetFileName(replayFile)}");
                await semaphore.WaitAsync();
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var metadata = BuildOrderReader.GetReplayMetadataFast(replayFile);
                        if (metadata != null && database != null)
                        {
                            database.AddOrUpdateReplay(
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
                            
                            _cacheManager.AddCachedFile(metadata.FilePath);
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
                            _outputProvider.RenderCacheProgress(processedCount, newReplayFiles.Count);
                        }
                        semaphore.Release();
                    }
                }));
            }

            // Wait for all tasks to complete
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            if (newReplaysAdded > 0)
            {
                _outputProvider.RenderSyncComplete(newReplaysAdded);
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayService] Sync complete, added {newReplaysAdded} new replays");

            // Notify that cache operation is complete so UI can refresh
            OnCacheOperationComplete?.Invoke();
        }        /// <summary>
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

        public List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10)
        {
            try
            {
                var database = new ReplayDatabase();
                return database.GetOpponentMatchHistory(yourPlayerName, opponentName, limit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error getting opponent match history: {ex}");
                return new List<(string, DateTime, string, string, string, string)>();
            }
        }
    }
}
