using System;
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
            var databasePath = Path.Combine(dbPath, "cache.db");

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
                var checkQuery = new Query("ReplayFiles")
                    .Where("ReplayFileLocation", metadata.ReplayFilePath)
                    .Select("Id");
                var checkCompiled = _compiler.Compile(checkQuery);

                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkCompiled.Sql;
                checkCommand.Parameters.Clear();
                for (int i = 0; i < checkCompiled.Bindings.Count; i++)
                {
                    checkCommand.Parameters.Add(new SQLiteParameter($"@p{i}", checkCompiled.Bindings[i] ?? DBNull.Value));
                }

                var existingId = checkCommand.ExecuteScalar();
                if (existingId != null)
                {
                    _logger.Debug("Replay already exists at location: {ReplayFilePath}", metadata.ReplayFilePath);
                    return Convert.ToInt64(existingId);
                }

                // Get player IDs (referenced via toon handle)
                long p1Id = InsertOrGetPlayer(connection, metadata.YourPlayer, metadata.YourPlayerId);
                long p2Id = InsertOrGetPlayer(connection, metadata.OpponentPlayer, metadata.OpponentPlayerId);
                var normalizedP1Toon = NormalizeToonForStorage(metadata.YourPlayerId);
                var normalizedP2Toon = NormalizeToonForStorage(metadata.OpponentPlayerId);

                // Insert new replay file using SqlKata
                var now = DateTime.UtcNow.ToString("O");
                var insertQuery = new Query("ReplayFiles").AsInsert(new Dictionary<string, object>
                {
                    ["Guid"] = metadata.ReplayGuid,
                    ["Map"] = metadata.Map ?? "Unknown",
                    ["P1Id"] = p1Id,
                    ["P2Id"] = p2Id,
                    ["Winner"] = DBNull.Value,
                    ["P1Toon"] = normalizedP1Toon,
                    ["P2Toon"] = normalizedP2Toon,
                    ["DeterministicGuid"] = metadata.ReplayGuid,
                    ["DatePlayedAt"] = metadata.GameDate.ToString("O"),
                    ["ReplayFileLocation"] = metadata.ReplayFilePath,
                    ["CreatedAt"] = now,
                    ["UpdatedAt"] = now
                });

                var insertCompiled = _compiler.Compile(insertQuery);
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = insertCompiled.Sql;
                insertCommand.Parameters.Clear();
                for (int i = 0; i < insertCompiled.Bindings.Count; i++)
                {
                    insertCommand.Parameters.Add(new SQLiteParameter($"@p{i}", insertCompiled.Bindings[i] ?? DBNull.Value));
                }
                insertCommand.ExecuteNonQuery();

                // Get the inserted ID using a separate command to avoid parameter binding issues
                using var lastIdCommand = connection.CreateCommand();
                lastIdCommand.CommandText = "SELECT last_insert_rowid()";
                var newId = lastIdCommand.ExecuteScalar();

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
                    var normalizedToonId = NormalizeToonForStorage(toonId);
                    var toon = string.IsNullOrEmpty(normalizedToonId)
                        ? $"toon-{Guid.NewGuid():N}".Substring(0, 20)
                        : normalizedToonId;

                    // Priority 1: Try to get by exact toon ID if provided (most specific match)
                    if (!string.IsNullOrEmpty(normalizedToonId))
                    {
                        var checkQuery = new Query("Players")
                            .Where("Toon", normalizedToonId)
                            .Select("Id");
                        var checkCompiled = _compiler.Compile(checkQuery);

                        using var checkCommand = connection.CreateCommand();
                        checkCommand.CommandText = checkCompiled.Sql;
                        checkCommand.Parameters.Clear();
                        for (int i = 0; i < checkCompiled.Bindings.Count; i++)
                        {
                            checkCommand.Parameters.Add(new SQLiteParameter($"@p{i}", checkCompiled.Bindings[i] ?? DBNull.Value));
                        }

                        var existingId = checkCommand.ExecuteScalar();
                        if (existingId != null)
                        {
                            _logger.Debug("Player already exists by toon: {Toon}", normalizedToonId);
                            return Convert.ToInt64(existingId);
                        }
                    }

                    // Priority 2: Try to get by BattleTag (good middle ground for identity)
                    var battleTagQuery = new Query("Players")
                        .Where("BattleTag", battleTag)
                        .Select("Id");
                    var battleTagCompiled = _compiler.Compile(battleTagQuery);

                    using var battleTagCheckCommand = connection.CreateCommand();
                    battleTagCheckCommand.CommandText = battleTagCompiled.Sql;
                    battleTagCheckCommand.Parameters.Clear();
                    for (int i = 0; i < battleTagCompiled.Bindings.Count; i++)
                    {
                        battleTagCheckCommand.Parameters.Add(new SQLiteParameter($"@p{i}", battleTagCompiled.Bindings[i] ?? DBNull.Value));
                    }

                    var battleTagId = battleTagCheckCommand.ExecuteScalar();
                    if (battleTagId != null)
                    {
                        _logger.Debug("Player already exists: {BattleTag}", battleTag);
                        return Convert.ToInt64(battleTagId);
                    }

                    // Priority 3: Try LIKE pattern match on toon suffix if a full toon ID is provided
                    // This handles cases where toon format changes slightly but suffix is stable
                    if (!string.IsNullOrEmpty(normalizedToonId))
                    {
                        var toonSuffix = GetToonSuffix(normalizedToonId);
                        if (!string.IsNullOrEmpty(toonSuffix) && !string.Equals(toonSuffix, normalizedToonId, StringComparison.Ordinal))
                        {
                            var suffixQuery = new Query("Players")
                                .WhereLike("Toon", $"%{toonSuffix}")
                                .Select("Id");
                            var suffixCompiled = _compiler.Compile(suffixQuery);

                            using var suffixCheckCommand = connection.CreateCommand();
                            suffixCheckCommand.CommandText = suffixCompiled.Sql;
                            suffixCheckCommand.Parameters.Clear();
                            for (int i = 0; i < suffixCompiled.Bindings.Count; i++)
                            {
                                suffixCheckCommand.Parameters.Add(new SQLiteParameter($"@p{i}", suffixCompiled.Bindings[i] ?? DBNull.Value));
                            }

                            var suffixId = suffixCheckCommand.ExecuteScalar();
                            if (suffixId != null)
                            {
                                _logger.Debug("Player already exists by toon suffix: {ToonSuffix}", toonSuffix);
                                return Convert.ToInt64(suffixId);
                            }
                        }
                    }

                    // Insert new player using SqlKata
                    var now = DateTime.UtcNow.ToString("O");

                    var insertQuery = new Query("Players").AsInsert(new Dictionary<string, object>
                    {
                        ["Nickname"] = nickname,
                        ["BattleTag"] = battleTag,
                        ["Toon"] = toon,
                        ["CreatedAt"] = now,
                        ["UpdatedAt"] = now
                    });

                    var insertCompiled = _compiler.Compile(insertQuery);
                    using var insertCommand = connection.CreateCommand();
                    insertCommand.CommandText = insertCompiled.Sql;
                    insertCommand.Parameters.Clear();
                    for (int i = 0; i < insertCompiled.Bindings.Count; i++)
                    {
                        insertCommand.Parameters.Add(new SQLiteParameter($"@p{i}", insertCompiled.Bindings[i] ?? DBNull.Value));
                    }

                    insertCommand.ExecuteNonQuery();

                    // Get the inserted ID using a separate command to avoid parameter binding issues
                    using var lastIdCommand = connection.CreateCommand();
                    lastIdCommand.CommandText = "SELECT last_insert_rowid()";
                    var newId = lastIdCommand.ExecuteScalar();

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

        private static string NormalizeToonForStorage(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var span = raw.AsSpan();
            Span<char> buffer = stackalloc char[span.Length];
            var idx = 0;
            foreach (var ch in span)
            {
                if (!char.IsControl(ch))
                {
                    buffer[idx++] = ch;
                }
            }

            return new string(buffer[..idx]).Trim();
        }

        private static string GetToonSuffix(string? toon)
        {
            var normalized = NormalizeToonForStorage(toon);
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            var dashIndex = normalized.IndexOf('-');
            if (dashIndex >= 0 && dashIndex + 1 < normalized.Length)
            {
                return normalized[(dashIndex + 1)..];
            }

            return normalized;
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
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
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
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
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
                var query = new Query("Replays")
                    .SelectRaw("MAX(ReplayDate)");
                var compiled = _compiler.Compile(query);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
                }

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
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
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
            var normalizedSuffix = GetToonSuffix(toonSuffix);
            if (string.IsNullOrEmpty(normalizedSuffix))
                return null;

            try
            {
                using var connection = CreateConnection();
                var query = new Query("Players")
                    .WhereLike("Toon", $"%{normalizedSuffix}")
                    .Select("Id")
                    .Limit(1);
                var compiled = _compiler.Compile(query);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
                }

                var result = command.ExecuteScalar();
                if (result != null)
                {
                    _logger.Debug("Found player by toon suffix: {ToonSuffix}", normalizedSuffix);
                    return Convert.ToInt64(result);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to find player by toon suffix: {ToonSuffix}", normalizedSuffix);
                return null;
            }
        }

        /// <summary>
        /// Find a player by complete toon ID.
        /// </summary>
        public long? FindPlayerByToon(string toonId)
        {
            var normalizedToon = NormalizeToonForStorage(toonId);
            if (string.IsNullOrEmpty(normalizedToon))
                return null;

            try
            {
                using var connection = CreateConnection();
                var query = new Query("Players")
                    .Where("Toon", normalizedToon)
                    .Select("Id");
                var compiled = _compiler.Compile(query);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
                }

                var result = command.ExecuteScalar();
                if (result != null)
                {
                    _logger.Debug("Found player by toon: {Toon}", normalizedToon);
                    return Convert.ToInt64(result);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to find player by toon: {Toon}", normalizedToon);
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
