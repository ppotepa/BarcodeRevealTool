using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace BarcodeRevealTool.Replay
{
    /// <summary>
    /// Manages in-memory replay cache for efficient sync operations.
    /// Loads all cached replay paths from database once, then uses in-memory diff
    /// to determine which files need to be added/removed.
    /// </summary>
    public class CacheManager
    {
        private HashSet<string> _cachedFilePaths = new(StringComparer.OrdinalIgnoreCase);
        private bool _isLoaded = false;

        public CacheManager()
        {
        }

        /// <summary>
        /// Load all cached file paths from database into memory (single database query).
        /// Call this once at startup after cache.lock exists.
        /// </summary>
        public void LoadCachedFilePaths(ReplayDatabase database)
        {
            if (_isLoaded)
                return;

            System.Diagnostics.Debug.WriteLine("[CacheManager] Loading cached file paths from database");
            var cachedReplays = database.GetAllCachedReplays();

            foreach (var replay in cachedReplays)
            {
                _cachedFilePaths.Add(replay.ReplayFilePath);
            }

            _isLoaded = true;
            System.Diagnostics.Debug.WriteLine($"[CacheManager] Loaded {_cachedFilePaths.Count} cached file paths from database");
        }

        /// <summary>
        /// Get list of new replay files that are on disk but not in cache.
        /// </summary>
        public List<string> GetNewReplayFiles(string[] allReplayFilesOnDisk)
        {
            return allReplayFilesOnDisk
                .Where(filePath => !_cachedFilePaths.Contains(filePath))
                .ToList();
        }

        /// <summary>
        /// Get list of replay files that are in cache but no longer exist on disk.
        /// </summary>
        public List<string> GetDeletedReplayFiles(string[] allReplayFilesOnDisk)
        {
            var diskPaths = new HashSet<string>(allReplayFilesOnDisk, StringComparer.OrdinalIgnoreCase);
            return _cachedFilePaths
                .Where(cachedPath => !diskPaths.Contains(cachedPath))
                .ToList();
        }

        /// <summary>
        /// Add a replay file path to the in-memory cache after it's been saved to database.
        /// </summary>
        public void AddCachedFile(string filePath)
        {
            _cachedFilePaths.Add(filePath);
        }

        /// <summary>
        /// Remove a replay file path from the in-memory cache.
        /// </summary>
        public void RemoveCachedFile(string filePath)
        {
            _cachedFilePaths.Remove(filePath);
        }

        /// <summary>
        /// Get total count of cached files.
        /// </summary>
        public int CachedFileCount => _cachedFilePaths.Count;

        /// <summary>
        /// Check if a specific file is already cached.
        /// </summary>
        public bool IsCached(string filePath)
        {
            return _cachedFilePaths.Contains(filePath);
        }

        /// <summary>
        /// Clear in-memory cache (for testing or forced refresh).
        /// </summary>
        public void Clear()
        {
            _cachedFilePaths.Clear();
            _isLoaded = false;
        }
    }
}
