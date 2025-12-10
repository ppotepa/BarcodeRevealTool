using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;
using BarcodeRevealTool.Persistence.Database;
using BarcodeRevealTool.Persistence.Replay;
using Serilog;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Manages cache state, validation, and synchronization with disk replay files.
    /// Provides a lock mechanism to ensure single-process access to the cache.
    /// </summary>
    public class CacheManager : ICacheManager
    {
        private readonly ReplayDatabase _database;
        private readonly ReplayCacheService _replayCacheService;
        private readonly ILogger _logger = Log.ForContext<CacheManager>();
        private FileStream? _lockFileStream;
        private readonly string _lockFilePath;
        private bool _isValid = false;
        private bool _initialized = false;
        private bool _fullSyncJustCompleted = false;

        public CacheManager(ReplayDatabase database, ReplayCacheService replayCacheService)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _replayCacheService = replayCacheService ?? throw new ArgumentNullException(nameof(replayCacheService));
            _lockFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
        }

        public bool WasFullSyncJustCompleted => _fullSyncJustCompleted;

        public void ResetFullSyncFlag() => _fullSyncJustCompleted = false;

        public async Task InitializeAsync()
        {
            if (_initialized)
            {
                _logger.Information("CacheManager already initialized");
                return;
            }

            try
            {
                _logger.Information("Initializing CacheManager...");

                // Attempt to acquire lock
                if (!await Task.Run(() => AcquireLock()))
                {
                    throw new InvalidOperationException("Failed to acquire cache lock. Another instance may be running.");
                }

                _isValid = true;
                _initialized = true;

                _logger.Information("CacheManager initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize CacheManager");
                throw;
            }
        }

        public async Task SyncFromDiskAsync(string replayFolder, bool recursive)
        {
            try
            {
                if (!Directory.Exists(replayFolder))
                {
                    _logger.Warning("Replay folder does not exist: {ReplayFolder}", replayFolder);
                    return;
                }

                _logger.Information("Syncing cache from disk: {ReplayFolder}", replayFolder);
                await _replayCacheService.InitializeCacheAsync();
                _fullSyncJustCompleted = true;
                _logger.Information("Cache synchronization completed");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync cache from disk");
                throw;
            }
        }

        public async Task SyncMissingReplaysAsync(string replayFolder, bool recursive)
        {
            try
            {
                if (!Directory.Exists(replayFolder))
                {
                    _logger.Warning("Replay folder does not exist for incremental sync: {ReplayFolder}", replayFolder);
                    return;
                }

                _logger.Information("Syncing missing replays from disk: {ReplayFolder}", replayFolder);
                await _replayCacheService.SyncMissingReplaysAsync();
                _logger.Information("Incremental replay synchronization completed");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync missing replays from disk");
                throw;
            }
        }

        public async Task SyncRecentReplayAsync(string replayFolder)
        {
            try
            {
                if (!Directory.Exists(replayFolder))
                {
                    _logger.Warning("Replay folder does not exist for recent sync: {ReplayFolder}", replayFolder);
                    return;
                }

                _logger.Information("Syncing most recent replay from disk: {ReplayFolder}", replayFolder);
                await _replayCacheService.SyncRecentReplayAsync();
                _logger.Information("Recent replay synchronization completed");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync most recent replay from disk");
                throw;
            }
        }

        public CacheStatistics GetStatistics()
        {
            try
            {
                return _database.GetCacheStatistics();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get cache statistics");
                return new CacheStatistics(0, 0, DateTime.MinValue);
            }
        }

        public bool IsCacheValid()
        {
            return _isValid && _initialized;
        }

        public bool IsCacheEmpty()
        {
            try
            {
                var stats = GetStatistics();
                return stats.TotalMatches == 0;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check if cache is empty");
                return true; // Assume empty if we can't check
            }
        }

        public IReadOnlyList<string> GetMissingReplayFiles(string[] diskFiles)
        {
            try
            {
                // This would compare disk files with cached metadata
                // For now, return empty as placeholder
                _logger.Debug("Checking for missing replay files");
                return Array.Empty<string>();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get missing replay files");
                return diskFiles;
            }
        }

        private bool AcquireLock()
        {
            const int maxRetries = 3;
            const int retryDelayMs = 500;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    _logger.Debug("Attempting to acquire cache lock (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);

                    _lockFileStream = new FileStream(
                        _lockFilePath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 1,
                        useAsync: true
                    );

                    _logger.Information("Cache lock acquired successfully");
                    return true;
                }
                catch (IOException ex)
                {
                    _logger.Warning(ex, "Failed to acquire cache lock (attempt {Attempt}/{MaxRetries})", i + 1, maxRetries);

                    if (i < maxRetries - 1)
                    {
                        Thread.Sleep(retryDelayMs);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Unexpected error acquiring cache lock");
                    return false;
                }
            }

            _logger.Error("Could not acquire cache lock after {MaxRetries} attempts", maxRetries);
            return false;
        }

        private void ReleaseLock()
        {
            try
            {
                if (_lockFileStream != null)
                {
                    _lockFileStream.Dispose();
                    _lockFileStream = null;

                    // Keep the cache.lock file on disk so future runs know the cache
                    // has been initialized. While the stream is open it still works
                    // as a mutex; once released we overwrite it with a timestamp.
                    File.WriteAllText(_lockFilePath, $"Initialized {DateTime.UtcNow:O}");

                    _logger.Information("Cache lock released");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error releasing cache lock");
            }
        }

        public void Dispose()
        {
            ReleaseLock();
            GC.SuppressFinalize(this);
        }

        ~CacheManager()
        {
            ReleaseLock();
        }
    }
}
