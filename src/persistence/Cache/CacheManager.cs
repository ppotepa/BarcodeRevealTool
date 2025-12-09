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

        public CacheManager(ReplayDatabase database, ReplayCacheService replayCacheService)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _replayCacheService = replayCacheService ?? throw new ArgumentNullException(nameof(replayCacheService));
            _lockFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
        }

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
                if (!AcquireLock())
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
                _logger.Information("Cache synchronization completed");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to sync cache from disk");
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

                    if (File.Exists(_lockFilePath))
                    {
                        File.Delete(_lockFilePath);
                    }

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
