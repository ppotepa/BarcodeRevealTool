using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Replay;
using Microsoft.Extensions.Configuration;

namespace BarcodeRevealTool.Services
{
    /// <summary>
    /// Concrete implementation of IReplayService using BuildOrderReader and ReplayDatabase
    /// Simplified caching: First run builds full cache, subsequent runs add missing files only.
    /// </summary>
    public class ReplayService : IReplayService
    {
        private readonly IOutputProvider _outputProvider;
        private readonly IConfiguration _configuration;
        private bool _initialized = false;
        private string CacheLockFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
        private string CacheValidationFile => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.validation");
        private const int CacheValidationIntervalMinutes = 60; // Re-scan for changes every 60 minutes

        public Action? OnCacheOperationComplete { get; set; }

        public ReplayService(IOutputProvider outputProvider, IConfiguration configuration)
        {
            _outputProvider = outputProvider;
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
                $"[ReplayService] System Info - Processors: {processorCount}, " +
                $"Optimal Parallelism: {optimalDegree}, " +
                $"Utilization: {(optimalDegree * 100) / processorCount}%"
            );

            return optimalDegree;
        }

        public async Task InitializeCacheAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayService] InitializeCacheAsync started");

            // Only run initialization once
            if (_initialized)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Already initialized, skipping");
                return;
            }

            // Initialize database
            BuildOrderReader.InitializeCache();
            var database = BuildOrderReader.GetDatabase();
            if (database == null)
            {
                _outputProvider.RenderError("Failed to initialize database");
                return;
            }

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            string userBattleTag;
            try
            {
                userBattleTag = EnsureConfiguredBattleTag(appSettings);
            }
            catch (InvalidOperationException ex)
            {
                _outputProvider.RenderError(ex.Message);
                return;
            }

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Replays folder not configured or doesn't exist");
                _initialized = true;
                OnCacheOperationComplete?.Invoke();
                return;
            }

            // Check if config has changed - force full rescan if it did
            bool configChanged = CheckAndUpdateConfigHash(database, appSettings);
            if (configChanged)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Configuration has changed (folder path or recursive flag), triggering full rescan");
                // Delete cache lock and validation files to force full rescan
                try
                {
                    File.Delete(CacheLockFile);
                    File.Delete(CacheValidationFile);
                }
                catch { /* ignore deletion errors */ }
            }

            // Check if cache exists
            bool cacheExists = File.Exists(CacheLockFile);

            if (!cacheExists)
            {
                // FIRST RUN: Build complete cache from scratch
                System.Diagnostics.Debug.WriteLine($"[ReplayService] No cache lock file, building complete cache");
                _outputProvider.RenderWarning("No cache found.");
                _outputProvider.RenderCacheInitializingMessage();

                var searchOption = (appSettings.Replays.Recursive == true)
                    ? SearchOption.AllDirectories
                    : SearchOption.TopDirectoryOnly;

                var allReplayFiles = Directory.GetFiles(
                    appSettings.Replays.Folder,
                    "*.SC2Replay",
                    searchOption);

                System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {allReplayFiles.Length} replay files on disk");

                if (allReplayFiles.Length > 0)
                {
                    _outputProvider.RenderWarning($"{allReplayFiles.Length} replays found.");
                    await ProcessReplaysAsync(database, allReplayFiles, allReplayFiles.Length, userBattleTag);
                    _outputProvider.RenderCacheComplete();
                }

                // Create cache lock file and validation marker
                File.WriteAllText(CacheLockFile, DateTime.UtcNow.ToString("O"));
                UpdateCacheValidationTime();
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache build complete, lock file created");
            }
            else
            {
                // SUBSEQUENT RUNS: Check if we need to re-scan for new files
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache lock file exists");

                bool shouldRescan = ShouldRescanForChanges();

                if (shouldRescan)
                {
                    // Only scan if validation interval has passed
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache validation interval exceeded, checking for missing files");

                    var searchOption = (appSettings.Replays.Recursive == true)
                        ? SearchOption.AllDirectories
                        : SearchOption.TopDirectoryOnly;

                    var allReplayFiles = Directory.GetFiles(
                        appSettings.Replays.Folder,
                        "*.SC2Replay",
                        searchOption);

                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {allReplayFiles.Length} replay files on disk");

                    var missingFiles = database.GetMissingReplayFiles(allReplayFiles);
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {missingFiles.Count} missing replay files");

                    if (missingFiles.Count > 0)
                    {
                        _outputProvider.RenderCacheSyncMessage();
                        await ProcessReplaysAsync(database, missingFiles.ToArray(), missingFiles.Count, userBattleTag);
                        _outputProvider.RenderSyncComplete(missingFiles.Count);
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReplayService] No new files, cache is current");
                    }

                    // Update validation time after check
                    UpdateCacheValidationTime();
                }
                else
                {
                    // Cache was recently validated, skip disk scan entirely
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache recently validated, skipping disk scan (instant startup)");
                }
            }

            _initialized = true;
            OnCacheOperationComplete?.Invoke();
        }

        /// <summary>
        /// Check if cache validation interval has passed.
        /// Returns true if we should re-scan disk for changes, false if cache is fresh enough.
        /// </summary>
        private bool ShouldRescanForChanges()
        {
            if (!File.Exists(CacheValidationFile))
            {
                // No validation file exists yet, should scan
                return true;
            }

            try
            {
                var content = File.ReadAllText(CacheValidationFile);
                if (DateTime.TryParse(content, out var lastValidationTime))
                {
                    var timeSinceValidation = DateTime.UtcNow - lastValidationTime;
                    bool shouldRescan = timeSinceValidation.TotalMinutes >= CacheValidationIntervalMinutes;
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache last validated {timeSinceValidation.TotalMinutes:F1} minutes ago (rescan interval: {CacheValidationIntervalMinutes}m) - Should rescan: {shouldRescan}");
                    return shouldRescan;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error reading validation time: {ex}");
                return true;
            }

            return true;
        }

        /// <summary>
        /// Update the cache validation timestamp.
        /// This prevents costly re-scans if tool is restarted within the validation interval.
        /// </summary>
        private void UpdateCacheValidationTime()
        {
            try
            {
                File.WriteAllText(CacheValidationFile, DateTime.UtcNow.ToString("O"));
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache validation time updated");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error updating validation time: {ex}");
            }
        }

        private string EnsureConfiguredBattleTag()
        {
            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);
            return EnsureConfiguredBattleTag(appSettings);
        }

        private static string EnsureConfiguredBattleTag(AppSettings appSettings)
        {
            var battleTag = appSettings.User?.BattleTag;
            if (string.IsNullOrWhiteSpace(battleTag))
            {
                throw new InvalidOperationException("Set 'barcodeReveal:user:battleTag' in appsettings.json to identify your account.");
            }

            return battleTag;
        }

        /// <summary>
        /// Process replays in parallel using optimal system resources.
        /// Loads all replay metadata into memory first for maximum performance.
        /// </summary>
        private async Task ProcessReplaysAsync(ReplayDatabase database, string[] replayFiles, int totalCount, string? userBattleTag = null)
        {
            int maxDegreeOfParallelism = GetOptimalDegreeOfParallelism();

            System.Diagnostics.Debug.WriteLine(
                $"[ReplayService] Starting cache build with {replayFiles.Length} files, " +
                $"{maxDegreeOfParallelism} parallel threads, userBattleTag={userBattleTag ?? "None"}"
            );

            // Stage 1: Load all replay metadata into memory (fast sequential scan)
            System.Diagnostics.Debug.WriteLine(
                $"[ReplayService] Stage 1: Loading {replayFiles.Length} replay metadata into memory..."
            );
            var replayMetadataList = await LoadReplayMetadataAsync(replayFiles);

            System.Diagnostics.Debug.WriteLine(
                $"[ReplayService] Stage 1 complete: Loaded {replayMetadataList.Count} replay metadata into memory"
            );

            // Stage 2: Process loaded metadata in parallel for database insertion
            System.Diagnostics.Debug.WriteLine(
                $"[ReplayService] Stage 2: Starting parallel database inserts with {maxDegreeOfParallelism} threads..."
            );
            _outputProvider.RenderWarning($"Processing {totalCount} replays with {maxDegreeOfParallelism} parallel threads...");

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
                        if (metadata != null && metadata.Players.Count >= 2)
                        {
                            string yourPlayer = string.Empty, opponentPlayer = string.Empty;
                            string yourRace = string.Empty, opponentRace = string.Empty;
                            string? yourPlayerId = null, opponentPlayerId = null;

                            // Determine You vs Opponent based on user battle tag
                            if (!string.IsNullOrEmpty(userBattleTag))
                            {
                                string normalizedUserTag = NormalizeConfiguredBattleTag(userBattleTag);
                                string normalizedPlayer1 = NormalizeConfiguredBattleTag(metadata.Players[0].BattleTag);
                                string normalizedPlayer2 = NormalizeConfiguredBattleTag(metadata.Players[1].BattleTag);

                                if (!string.IsNullOrEmpty(normalizedPlayer1) &&
                                    (normalizedPlayer1.Equals(normalizedUserTag, StringComparison.OrdinalIgnoreCase) ||
                                     normalizedUserTag.Contains(normalizedPlayer1)))
                                {
                                    yourPlayer = metadata.Players[0].Name;
                                    yourRace = metadata.Players[0].Race;
                                    yourPlayerId = metadata.Players[0].PlayerId;
                                    opponentPlayer = metadata.Players[1].Name;
                                    opponentRace = metadata.Players[1].Race;
                                    opponentPlayerId = metadata.Players[1].PlayerId;
                                }
                                else if (!string.IsNullOrEmpty(normalizedPlayer2) &&
                                         (normalizedPlayer2.Equals(normalizedUserTag, StringComparison.OrdinalIgnoreCase) ||
                                          normalizedUserTag.Contains(normalizedPlayer2)))
                                {
                                    yourPlayer = metadata.Players[1].Name;
                                    yourRace = metadata.Players[1].Race;
                                    yourPlayerId = metadata.Players[1].PlayerId;
                                    opponentPlayer = metadata.Players[0].Name;
                                    opponentRace = metadata.Players[0].Race;
                                    opponentPlayerId = metadata.Players[0].PlayerId;
                                }
                                else
                                {
                                    yourPlayer = metadata.Players[0].Name;
                                    yourRace = metadata.Players[0].Race;
                                    yourPlayerId = metadata.Players[0].PlayerId;
                                    opponentPlayer = metadata.Players[1].Name;
                                    opponentRace = metadata.Players[1].Race;
                                    opponentPlayerId = metadata.Players[1].PlayerId;
                                }
                            }
                            else
                            {
                                yourPlayer = metadata.Players[0].Name;
                                yourRace = metadata.Players[0].Race;
                                yourPlayerId = metadata.Players[0].PlayerId;
                                opponentPlayer = metadata.Players[1].Name;
                                opponentRace = metadata.Players[1].Race;
                                opponentPlayerId = metadata.Players[1].PlayerId;
                            }

                            // Populate Opponents table (skip current user if known)
                            database.UpsertOpponents(metadata.Players, userBattleTag);

                            var map = !string.IsNullOrEmpty(metadata.Map) ? metadata.Map : "Unknown";

                            database.AddOrUpdateReplay(
                                yourPlayer,
                                opponentPlayer,
                                map,
                                yourRace,
                                opponentRace,
                                metadata.GameDate,
                                metadata.FilePath,
                                metadata.SC2ClientVersion,
                                yourPlayerId,
                                opponentPlayerId
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReplayService] Error processing replay: {ex}");
                        _outputProvider.RenderWarning($"Failed to cache {Path.GetFileName(filePath)}: {ex.Message}");
                    }
                    finally
                    {
                        lock (lockObj)
                        {
                            processedCount++;
                            _outputProvider.RenderCacheProgress(processedCount, totalCount);
                        }
                        semaphore.Release();
                    }
                }));
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            // Print newline after progress bar completes
            System.Console.WriteLine();
            System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache processing complete. Processed {processedCount} of {totalCount} replays");
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

            _outputProvider.RenderWarning("Copying replays to memory...");

            await Task.Run(() =>
            {
                foreach (var replayFile in replayFiles)
                {
                    try
                    {
                        var metadata = BuildOrderReader.GetReplayMetadataFast(replayFile);
                        metadataList.Add((replayFile, metadata));
                        loadedCount++;

                        // Show progress bar for metadata loading
                        if (loadedCount % 5 == 0 || loadedCount == replayFiles.Length)
                        {
                            double percent = (double)loadedCount / replayFiles.Length;
                            int barLength = 20;
                            int filledLength = (int)(barLength * percent);
                            string bar = new string('█', filledLength) + new string('░', barLength - filledLength);
                            string progressText = $"▓ {loadedCount}/{replayFiles.Length} {bar} {(percent * 100):F0}%";

                            System.Console.Write($"\r[!] Copying: {progressText,-75}");
                            System.Console.Out.Flush();
                        }

                        // Log progress every 100 files
                        if (loadedCount % 100 == 0)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[ReplayService] Loaded {loadedCount}/{replayFiles.Length} replay metadata into memory"
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[ReplayService] Error loading metadata for {Path.GetFileName(replayFile)}: {ex.Message}"
                        );
                        metadataList.Add((replayFile, null));
                    }
                }
            });

            System.Console.WriteLine();  // Newline after progress bar
            _outputProvider.RenderWarning($"Copied {loadedCount} replays to memory.");

            return metadataList;
        }
        public async Task SyncReplaysFromDiskAsync()
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayService] SyncReplaysFromDiskAsync started");

            // Skip disk scan if cache was recently validated
            if (!ShouldRescanForChanges())
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache recently validated, skipping disk scan for sync");
                return;
            }

            var appSettings = new AppSettings();
            _configuration.GetSection("barcodeReveal").Bind(appSettings);

            if (appSettings?.Replays?.Folder == null || !Directory.Exists(appSettings.Replays.Folder))
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Replays folder not configured or doesn't exist");
                return;
            }

            string userBattleTag;
            try
            {
                userBattleTag = EnsureConfiguredBattleTag();
            }
            catch (InvalidOperationException ex)
            {
                _outputProvider.RenderError(ex.Message);
                return;
            }

            // Get all replay files from disk
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
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Failed to get database");
                return;
            }

            // Get only missing files (using efficient database query)
            var missingFiles = database.GetMissingReplayFiles(allReplayFiles);
            System.Diagnostics.Debug.WriteLine($"[ReplayService] Found {missingFiles.Count} missing replay files");

            if (missingFiles.Count == 0)
            {
                // Cache is current, nothing to sync
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Cache is current, no syncing needed");
                UpdateCacheValidationTime();
                return;
            }

            // Show sync message and process only the missing files
            _outputProvider.RenderCacheSyncMessage();
            await ProcessReplaysAsync(database, missingFiles.ToArray(), missingFiles.Count, userBattleTag);
            _outputProvider.RenderSyncComplete(missingFiles.Count);
            UpdateCacheValidationTime();
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
                    string userBattleTag;
                    try
                    {
                        userBattleTag = EnsureConfiguredBattleTag();
                    }
                    catch (InvalidOperationException ex)
                    {
                        _outputProvider.RenderError(ex.Message);
                        return;
                    }

                    database.CacheMetadata(metadata, userBattleTag);
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

        public List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName, string? winner, string replayFilePath)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10)
        {
            try
            {
                var database = new ReplayDatabase();

                // Validate and normalize player names against known user accounts
                var validatedYourName = database.ValidateAndNormalizePlayerName(yourPlayerName);
                var validatedOpponentName = database.ValidateAndNormalizePlayerName(opponentName);

                System.Diagnostics.Debug.WriteLine($"[ReplayService] GetOpponentMatchHistory: original names: you='{yourPlayerName}', opponent='{opponentName}' -> validated: you='{validatedYourName}', opponent='{validatedOpponentName}'");

                return database.GetOpponentMatchHistory(validatedYourName, validatedOpponentName, limit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error getting opponent match history: {ex}");
                return new List<(string, DateTime, string, string, string, string, string?, string)>();
            }
        }

        public List<(double timeSeconds, string kind, string name)>?
            GetOpponentLastBuildOrder(string opponentName, int limit = 20)
        {
            try
            {
                var database = new ReplayDatabase();
                return database.GetOpponentLastBuildOrder(opponentName, limit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error getting opponent's last build order: {ex}");
                return null;
            }
        }

        /// <summary>
        /// Generate SHA256 hash of config settings (folder path + recursive flag).
        /// Used to detect when configuration has changed and trigger cache rescan.
        /// </summary>
        private string GenerateConfigHash(AppSettings appSettings)
        {
            var configString = $"{appSettings.Replays?.Folder}|{appSettings.Replays?.Recursive ?? false}";
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(configString));
                return Convert.ToHexString(hashedBytes);
            }
        }

        /// <summary>
        /// Check if configuration has changed since last run.
        /// Returns true if config differs (triggering full rescan), false if same.
        /// Updates stored hash if config is current.
        /// </summary>
        private bool CheckAndUpdateConfigHash(ReplayDatabase database, AppSettings appSettings)
        {
            try
            {
                var currentHash = GenerateConfigHash(appSettings);
                var folderPath = appSettings.Replays?.Folder ?? string.Empty;
                var recursive = appSettings.Replays?.Recursive ?? false;

                var storedMetadata = database.GetConfigMetadata();

                if (storedMetadata == null)
                {
                    // First run - store current config
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] First run - storing initial config hash");
                    database.UpdateConfigMetadata(currentHash, folderPath, recursive);
                    return false;
                }

                if (storedMetadata.Value.hash != currentHash)
                {
                    // Config changed - update stored hash
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Config changed! Old hash: {storedMetadata.Value.hash}, New hash: {currentHash}");
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Old folder: {storedMetadata.Value.folderPath}, New folder: {folderPath}");
                    System.Diagnostics.Debug.WriteLine($"[ReplayService] Old recursive: {storedMetadata.Value.recursive}, New recursive: {recursive}");

                    database.UpdateConfigMetadata(currentHash, folderPath, recursive);
                    return true; // Signal that config changed
                }

                // Config unchanged
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Config unchanged - no rescan needed");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error checking config hash: {ex}");
                return false; // Don't trigger rescan on error
            }
        }

        private static string NormalizeConfiguredBattleTag(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('_', '#').Trim().ToLowerInvariant();
        }

        public List<(string yourName, string opponentName, string yourRace, string opponentRace, DateTime gameDate, string map)>
            GetGamesByOpponentId(string yourPlayerId, string opponentPlayerId, int limit = 100)
        {
            try
            {
                var database = new ReplayDatabase();
                return database.GetGamesByOpponentId(yourPlayerId, opponentPlayerId, limit);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayService] Error getting games by opponent ID: {ex}");
                return new List<(string, string, string, string, DateTime, string)>();
            }
        }
    }
}

