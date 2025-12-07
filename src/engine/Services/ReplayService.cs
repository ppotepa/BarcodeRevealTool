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

            // Initialize the cache/database
            BuildOrderReader.InitializeCache();

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            // Scan replay folder and populate cache
            if (appSettings?.Replays?.Folder != null)
            {
                var searchOption = (appSettings.Replays.Recursive == true)
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var replayFiles = Directory.GetFiles(
                    appSettings.Replays.Folder,
                    "*.SC2Replay",
                    searchOption);

                // Use parallel processing with bounded concurrency (4 concurrent decoders)
                int maxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2);
                var semaphore = new SemaphoreSlim(maxDegreeOfParallelism);
                var database = BuildOrderReader.GetDatabase();

                var tasks = new List<Task>();
                int processedCount = 0;
                var lockObj = new object();

                // Scan all replays to populate the cache in parallel
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

            // Create cache lock file to prevent re-scanning on future startups
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
    }
}
