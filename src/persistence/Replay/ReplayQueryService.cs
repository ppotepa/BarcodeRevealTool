using BarcodeRevealTool.Persistence.Schema;
using Serilog;
using SqlKata;
using SqlKata.Compilers;
using System.Data.SQLite;

namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Service for querying and managing replay records in the database.
    /// Wraps all replay-related database operations with SqlKata queries.
    /// </summary>
    public class ReplayQueryService
    {
        private readonly string _connectionString;
        private readonly SqliteCompiler _compiler = new();
        private readonly ILogger _logger = Log.ForContext<ReplayQueryService>();

        // Lock for thread-safe player insert operations to prevent database lock errors
        private static readonly object _playerInsertLock = new();

        public ReplayQueryService(string? customDatabasePath = null)
        {
            var dbPath = customDatabasePath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "_db"
            );

            Directory.CreateDirectory(dbPath);
            var databasePath = Path.Combine(dbPath, "replays.db");

            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                JournalMode = SQLiteJournalModeEnum.Wal,
                CacheSize = 2000,
                Pooling = true,
                BusyTimeout = 5000
            };
            _connectionString = builder.ToString();

            InitializeSchema();
        }

        private SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString)
            {
                DefaultTimeout = 5
            };
            connection.Open();
            return connection;
        }

        private void InitializeSchema()
        {
            using var connection = CreateConnection();

            _logger.Information("Initializing replay database schema");

            try
            {
                SchemaLoader.ExecuteSchemas(connection, "RunInfo.sql", "Players.sql", "ReplayFiles.sql");
                _logger.Information("Replay database schema initialized");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize replay database schema");
                throw;
            }
        }

        public long AddOrUpdateReplay(ReplayMetadata metadata)
        {
            try
            {
                using var connection = CreateConnection();

                // Check if replay already exists by ReplayFileLocation (the UNIQUE constraint column)
                // This is more reliable than Guid since it's the physical file path
                var checkSql = "SELECT Id FROM ReplayFiles WHERE ReplayFileLocation = @location";
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkSql;
                checkCommand.Parameters.AddWithValue("@location", metadata.ReplayFilePath);

                var existingId = checkCommand.ExecuteScalar();
                if (existingId != null)
                {
                    _logger.Debug("Replay already exists at location: {ReplayFilePath}", metadata.ReplayFilePath);
                    return Convert.ToInt64(existingId);
                }

                // Ensure we have players - create or get player IDs
                long p1Id = InsertOrGetPlayer(connection, metadata.YourPlayer, metadata.YourPlayerId);
                long p2Id = InsertOrGetPlayer(connection, metadata.OpponentPlayer, metadata.OpponentPlayerId);

                // Insert new replay file using raw SQL
                var now = DateTime.UtcNow.ToString("O");
                var insertSql = @"INSERT INTO ReplayFiles 
                    (Guid, Map, P1Id, P2Id, Winner, P1Toon, P2Toon, DeterministicGuid, DatePlayedAt, ReplayFileLocation, CreatedAt, UpdatedAt)
                    VALUES (@guid, @map, @p1Id, @p2Id, @winner, @p1Toon, @p2Toon, @detGuid, @datePlayedAt, @location, @createdAt, @updatedAt)";

                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insertSql;
                insertCommand.Parameters.AddWithValue("@guid", metadata.ReplayGuid);
                insertCommand.Parameters.AddWithValue("@map", metadata.Map ?? "Unknown");
                insertCommand.Parameters.AddWithValue("@p1Id", p1Id);
                insertCommand.Parameters.AddWithValue("@p2Id", p2Id);
                insertCommand.Parameters.AddWithValue("@winner", DBNull.Value);
                insertCommand.Parameters.AddWithValue("@p1Toon", metadata.YourPlayerId ?? string.Empty);
                insertCommand.Parameters.AddWithValue("@p2Toon", metadata.OpponentPlayerId ?? string.Empty);
                insertCommand.Parameters.AddWithValue("@detGuid", metadata.ReplayGuid);
                insertCommand.Parameters.AddWithValue("@datePlayedAt", metadata.GameDate.ToString("O"));
                insertCommand.Parameters.AddWithValue("@location", metadata.ReplayFilePath);
                insertCommand.Parameters.AddWithValue("@createdAt", now);
                insertCommand.Parameters.AddWithValue("@updatedAt", now);

                insertCommand.ExecuteNonQuery();

                // Get the inserted ID
                insertCommand.CommandText = "SELECT last_insert_rowid()";
                var newId = insertCommand.ExecuteScalar();

                _logger.Debug("Stored replay: {YourPlayer} vs {OpponentPlayer} on {Map}",
                    metadata.YourPlayer, metadata.OpponentPlayer, metadata.Map);

                return Convert.ToInt64(newId ?? 0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add/update replay: {ReplayGuid}", metadata.ReplayGuid);
                return 0;
            }
        }

        /// <summary>
        /// Insert a player or return existing player ID.
        /// Uses lock to prevent SQLite "database is locked" errors during concurrent writes.
        /// </summary>
        private long InsertOrGetPlayer(SQLiteConnection connection, string nickname, string? toonId)
        {
            lock (_playerInsertLock)
            {
                try
                {
                    if (string.IsNullOrEmpty(nickname))
                        nickname = "Unknown";

                    var battleTag = ExtractBattleTag(nickname);
                    var toon = toonId ?? $"toon-{Guid.NewGuid():N}".Substring(0, 20);

                    // Priority 1: Try to get by exact toon ID if provided (most specific match)
                    if (!string.IsNullOrEmpty(toonId))
                    {
                        var checkSql = "SELECT Id FROM Players WHERE Toon = @toon";
                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = checkSql;
                        checkCommand.Parameters.AddWithValue("@toon", toonId);

                        var existingId = checkCommand.ExecuteScalar();
                        if (existingId != null)
                        {
                            _logger.Debug("Player already exists by toon: {Toon}", toonId);
                            return Convert.ToInt64(existingId);
                        }
                    }

                    // Priority 2: Try to get by BattleTag (good middle ground for identity)
                    var battleTagCheckSql = "SELECT Id FROM Players WHERE BattleTag = @battleTag";
                    using var battleTagCheckCommand = connection.CreateCommand();
                    battleTagCheckCommand.CommandText = battleTagCheckSql;
                    battleTagCheckCommand.Parameters.AddWithValue("@battleTag", battleTag);

                    var battleTagId = battleTagCheckCommand.ExecuteScalar();
                    if (battleTagId != null)
                    {
                        _logger.Debug("Player already exists: {BattleTag}", battleTag);
                        return Convert.ToInt64(battleTagId);
                    }

                    // Priority 3: Try LIKE pattern match on toon suffix if a full toon ID is provided
                    // This handles cases where toon format changes slightly but suffix is stable
                    if (!string.IsNullOrEmpty(toonId) && toonId.Contains('-') && toonId.Length > 2)
                    {
                        // Extract suffix from toon (e.g., "S2-1-11057632" from "2-S2-1-11057632")
                        var suffixStart = toonId.IndexOf('-') + 1;
                        if (suffixStart < toonId.Length)
                        {
                            var toonSuffix = toonId.Substring(suffixStart);
                            var suffixCheckSql = "SELECT Id FROM Players WHERE Toon LIKE '%' || @suffix";
                            using var suffixCheckCommand = connection.CreateCommand();
                            suffixCheckCommand.CommandText = suffixCheckSql;
                            suffixCheckCommand.Parameters.AddWithValue("@suffix", toonSuffix);

                            var suffixId = suffixCheckCommand.ExecuteScalar();
                            if (suffixId != null)
                            {
                                _logger.Debug("Player already exists by toon suffix: {ToonSuffix}", toonSuffix);
                                return Convert.ToInt64(suffixId);
                            }
                        }
                    }

                    // Insert new player using raw SQL
                    var now = DateTime.UtcNow.ToString("O");

                    var insertSql = @"INSERT INTO Players (Nickname, BattleTag, Toon, CreatedAt, UpdatedAt)
                        VALUES (@nickname, @battleTag, @toon, @createdAt, @updatedAt)";

                    using var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = insertSql;
                    insertCommand.Parameters.AddWithValue("@nickname", nickname);
                    insertCommand.Parameters.AddWithValue("@battleTag", battleTag);
                    insertCommand.Parameters.AddWithValue("@toon", toon);
                    insertCommand.Parameters.AddWithValue("@createdAt", now);
                    insertCommand.Parameters.AddWithValue("@updatedAt", now);

                    insertCommand.ExecuteNonQuery();

                    insertCommand.CommandText = "SELECT last_insert_rowid()";
                    var newId = insertCommand.ExecuteScalar();

                    _logger.Debug("Created player: {Nickname} ({Toon})", nickname, toon);
                    return Convert.ToInt64(newId ?? 0);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to insert/get player: {Nickname}", nickname);
                    return 0;
                }
            }
        }

        /// <summary>
        /// Determine winner from metadata (if available).
        /// </summary>
        private string? DetermineWinner(ReplayMetadata metadata)
        {
            // This would be populated from actual replay parsing
            // For now return null - can be enhanced when full replay parsing is implemented
            return null;
        }

        /// <summary>
        /// Extract battle tag from player nickname.
        /// Formats: "Name#12345" or "Name_12345"
        /// </summary>
        private string ExtractBattleTag(string nickname)
        {
            if (string.IsNullOrEmpty(nickname))
                return "Unknown#0000";

            // Look for # or _
            var hashIdx = nickname.LastIndexOf('#');
            var underscoreIdx = nickname.LastIndexOf('_');

            if (hashIdx > 0)
                return nickname.Substring(0, hashIdx) + nickname.Substring(hashIdx);
            if (underscoreIdx > 0)
                return nickname.Substring(0, underscoreIdx).Replace('_', '#') + "#" + nickname.Substring(underscoreIdx + 1);

            return nickname + "#0000";
        }

        public List<string> GetMissingReplayFiles(string[] allReplayFilesOnDisk)
        {
            try
            {
                if (allReplayFilesOnDisk.Length == 0)
                {
                    _logger.Debug("No replay files on disk to check");
                    return new List<string>();
                }

                using var connection = CreateConnection();

                // Get all cached file paths that ARE in the disk file list using SQL IN clause
                // This is more efficient than loading all cached files into memory
                var cachedFilesQuery = new Query("Replays")
                    .Select("ReplayFilePath")
                    .WhereIn("ReplayFilePath", allReplayFilesOnDisk.ToList());

                var compiled = _compiler.Compile(cachedFilesQuery);
                var cachedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var filePath = reader["ReplayFilePath"]?.ToString();
                    if (filePath != null)
                    {
                        cachedFiles.Add(filePath);
                    }
                }

                // Missing files are those on disk that are NOT in the cache
                var missingFiles = allReplayFilesOnDisk
                    .Where(f => !cachedFiles.Contains(f))
                    .ToList();

                _logger.Debug("Checked {TotalCount} disk files. Found {MissingCount} missing from cache",
                    allReplayFilesOnDisk.Length, missingFiles.Count);

                return missingFiles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get missing replay files");
                return allReplayFilesOnDisk.ToList();
            }
        }

        public bool IsReplayInCache(string replayFilePath)
        {
            try
            {
                using var connection = CreateConnection();
                var query = new Query("Replays")
                    .Where("ReplayFilePath", replayFilePath)
                    .Select("Id");

                var compiled = _compiler.Compile(query);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                var result = command.ExecuteScalar();
                bool isInCache = result != null;

                _logger.Debug("Replay {ReplayFile} is {Status}",
                    Path.GetFileName(replayFilePath), isInCache ? "cached" : "new");

                return isInCache;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check if replay is cached: {ReplayFile}", replayFilePath);
                return false;
            }
        }

        /// <summary>
        /// Get the most recent replay date from the cache.
        /// Used to check if new replays should be added based on file modification time.
        /// </summary>
        public DateTime? GetMostRecentReplayDate()
        {
            try
            {
                using var connection = CreateConnection();
                var sql = "SELECT MAX(ReplayDate) FROM Replays";
                using var command = connection.CreateCommand();
                command.CommandText = sql;

                var result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    if (DateTime.TryParse(result.ToString(), out var date))
                    {
                        _logger.Debug("Most recent replay in cache is from {Date}", date);
                        return date;
                    }
                }

                _logger.Debug("No replays found in cache");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get most recent replay date");
                return null;
            }

        }

        public int GetReplayCount()
        {
            try
            {
                using var connection = CreateConnection();
                using var command = connection.CreateCommand();

                var query = new Query("Replays").AsCount();
                var compiled = _compiler.Compile(query);

                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                var result = command.ExecuteScalar();
                return Convert.ToInt32(result ?? 0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get replay count");
                return 0;
            }
        }

        /// <summary>
        /// Find a player by toon suffix using LIKE pattern matching.
        /// Useful for matching partial or variant toon formats.
        /// </summary>
        public long? FindPlayerByToonSuffix(string toonSuffix)
        {
            try
            {
                if (string.IsNullOrEmpty(toonSuffix))
                    return null;

                using var connection = CreateConnection();
                var sql = "SELECT Id FROM Players WHERE Toon LIKE '%' || @suffix LIMIT 1";
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@suffix", toonSuffix);

                var result = command.ExecuteScalar();
                if (result != null)
                {
                    _logger.Debug("Found player by toon suffix: {ToonSuffix}", toonSuffix);
                    return Convert.ToInt64(result);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to find player by toon suffix: {ToonSuffix}", toonSuffix);
                return null;
            }
        }

        /// <summary>
        /// Find a player by complete toon ID.
        /// </summary>
        public long? FindPlayerByToon(string toonId)
        {
            try
            {
                if (string.IsNullOrEmpty(toonId))
                    return null;

                using var connection = CreateConnection();
                var sql = "SELECT Id FROM Players WHERE Toon = @toon";
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.Parameters.AddWithValue("@toon", toonId);

                var result = command.ExecuteScalar();
                if (result != null)
                {
                    _logger.Debug("Found player by toon: {Toon}", toonId);
                    return Convert.ToInt64(result);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to find player by toon: {Toon}", toonId);
                return null;
            }
        }

        private static string ComputeFileHash(string filePath)
        {
            try
            {
                using var sha256 = System.Security.Cryptography.SHA256.Create();
                using var stream = File.OpenRead(filePath);
                var hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
