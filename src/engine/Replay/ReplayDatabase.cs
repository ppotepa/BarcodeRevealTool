using System.Collections.Generic;
using System.Data.SQLite;
using System.Reflection;
using System.Linq;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;

namespace BarcodeRevealTool.Replay
{
    /// <summary>
    /// Main database for storing and retrieving replay information and build orders.
    /// </summary>
    public class ReplayDatabase
    {
        private readonly string _databasePath;
        private const string DatabaseFileName = "cache.db";
        private readonly string _connectionString;

        public ReplayDatabase(string? customPath = null)
        {
            var dbPath = customPath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "_db"
            );

            Directory.CreateDirectory(dbPath);
            _databasePath = Path.Combine(dbPath, DatabaseFileName);
            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = _databasePath,
                JournalMode = SQLiteJournalModeEnum.Wal,
                CacheSize = 2000,
                Pooling = true,
                BusyTimeout = 5000
            };
            _connectionString = builder.ToString();

            InitializeDatabase();
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

        private void LogQuery(SqlKata.Query query, string operation = "SELECT")
        {
            try
            {
                var compiler = new SqliteCompiler();
                var compiled = compiler.Compile(query);

                // Build full SQL with actual parameter values
                string fullSql = compiled.Sql;
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    var binding = compiled.Bindings[i];
                    string paramValue;

                    if (binding == null)
                    {
                        paramValue = "NULL";
                    }
                    else if (binding is string)
                    {
                        paramValue = $"'{binding}'";
                    }
                    else if (binding is bool)
                    {
                        paramValue = ((bool)binding) ? "1" : "0";
                    }
                    else if (binding is DateTime dt)
                    {
                        paramValue = $"'{dt:O}'";
                    }
                    else
                    {
                        paramValue = binding.ToString() ?? "NULL";
                    }

                    fullSql = fullSql.Replace($"@p{i}", paramValue);
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.Query] {operation}: {fullSql}");
            }
            catch
            {
                // If logging fails, don't break the query execution
            }
        }

        private void LogRawSql(string sql)
        {
            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.SQL] {sql}");
        }

        private QueryFactory CreateQueryFactory()
        {
            var connection = CreateConnection();
            var compiler = new SqliteCompiler();
            return new QueryFactory(connection, compiler);
        }

        private void InitializeDatabase()
        {
            using var queryFactory = CreateQueryFactory();

            // Read and execute the schema from embedded resource
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BarcodeRevealTool.Engine.Replay.sql.schema.sqlite";

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
            {
                throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
            }

            using var reader = new StreamReader(stream);
            var schemaSql = reader.ReadToEnd();

            // Execute schema statements one by one and tolerate statements that fail
            // (this helps when upgrading an existing DB that may be missing columns)
            var statements = schemaSql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawStmt in statements)
            {
                var stmt = rawStmt.Trim();
                if (string.IsNullOrWhiteSpace(stmt))
                    continue;

                try
                {
                    queryFactory.Statement(stmt + ";");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Schema statement failed (ignored): {ex.Message}. Statement start: {stmt.Substring(0, Math.Min(200, stmt.Length))}");
                }
            }

            // Apply backwards-compatibility migrations
            ApplySchemaMigrations(queryFactory);
        }

        private void ApplySchemaMigrations(QueryFactory queryFactory)
        {
            try
            {
                // Migration 1: Add NickName column to UserAccounts if it doesn't exist
                try
                {
                    queryFactory.Statement("ALTER TABLE UserAccounts ADD COLUMN NickName TEXT;");
                    System.Diagnostics.Debug.WriteLine("[ReplayDatabase] Applied migration: Added NickName to UserAccounts");
                }
                catch
                {
                    // Column likely already exists
                }

                // Migration 2: Add DisplayName column to Opponents if it doesn't exist
                try
                {
                    queryFactory.Statement("ALTER TABLE Opponents ADD COLUMN DisplayName TEXT;");
                    System.Diagnostics.Debug.WriteLine("[ReplayDatabase] Applied migration: Added DisplayName to Opponents");
                }
                catch
                {
                    // Column likely already exists
                }

                // Migration 3: Add NickName column to Opponents if it doesn't exist
                try
                {
                    queryFactory.Statement("ALTER TABLE Opponents ADD COLUMN NickName TEXT DEFAULT '';");
                    System.Diagnostics.Debug.WriteLine("[ReplayDatabase] Applied migration: Added NickName to Opponents");
                }
                catch
                {
                    // Column likely already exists
                }

                // Migration 4: Add NormalizedBattleTag column to Opponents if it doesn't exist
                try
                {
                    queryFactory.Statement("ALTER TABLE Opponents ADD COLUMN NormalizedBattleTag TEXT DEFAULT '';");
                    System.Diagnostics.Debug.WriteLine("[ReplayDatabase] Applied migration: Added NormalizedBattleTag to Opponents");
                }
                catch
                {
                    // Column likely already exists
                }

                // Update DisplayName for Opponents
                try
                {
                    queryFactory.Statement("UPDATE Opponents SET DisplayName = COALESCE(DisplayName, Name) WHERE DisplayName IS NULL OR DisplayName = '';");
                }
                catch
                {
                    // Table might be empty or column updates failed
                }

                // Update NickName based on DisplayName
                try
                {
                    queryFactory.Statement("UPDATE Opponents SET NickName = CASE WHEN NickName IS NULL OR NickName = '' THEN DisplayName ELSE NickName END;");
                }
                catch
                {
                    // Table might be empty
                }

                // Normalize BattleTag (replace _ with #)
                try
                {
                    queryFactory.Statement("UPDATE Opponents SET BattleTag = REPLACE(IFNULL(BattleTag, ''), '_', '#');");
                }
                catch
                {
                    // Update might fail if column doesn't exist or table is empty
                }

                // Update NormalizedBattleTag
                try
                {
                    queryFactory.Statement(@"UPDATE Opponents SET NormalizedBattleTag = CASE
                        WHEN NormalizedBattleTag IS NULL OR NormalizedBattleTag = '' THEN REPLACE(IFNULL(BattleTag, ''), '_', '#')
                        ELSE NormalizedBattleTag
                    END;");
                }
                catch
                {
                    // Update might fail if column doesn't exist or table is empty
                }

                System.Diagnostics.Debug.WriteLine("[ReplayDatabase] Schema migrations completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Error applying migrations: {ex.Message}");
            }
        }
        /// Add or update a replay record in the database (INSERT ONLY - never deletes).
        /// </summary>
        public long AddOrUpdateReplay(string player1, string player2, string map, string race1, string race2,
            DateTime gameDate, string replayFilePath, string? sc2ClientVersion = null, string? player1Id = null, string? player2Id = null, string? winner = null)
        {
            try
            {
                // Compute deterministic GUID based on replay name and game date
                var replayGuid = ReplayMetadata.ComputeDeterministicGuid(
                    Path.GetFileName(replayFilePath),
                    gameDate
                );

                using var queryFactory = CreateQueryFactory();

                var existing = queryFactory.Query("Replays")
                    .Select("Id")
                    .Where("ReplayGuid", replayGuid.ToString())
                    .FirstOrDefault<long?>();

                if (existing.HasValue)
                {
                    return existing.Value;
                }

                var fileHash = ComputeFileHash(replayFilePath);
                var now = DateTime.UtcNow.ToString("O");

                var insertData = new
                {
                    ReplayGuid = replayGuid.ToString(),
                    You = player1,
                    Opponent = player2,
                    YouId = player1Id,
                    OpponentId = player2Id,
                    Map = map,
                    YourRace = race1,
                    OpponentRace = race2,
                    GameDate = gameDate.ToString("O"),
                    ReplayFilePath = replayFilePath,
                    FileHash = fileHash,
                    SC2ClientVersion = sc2ClientVersion,
                    Winner = winner,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                LogRawSql($"INSERT INTO Replays (ReplayGuid, You, Opponent, YouId, OpponentId, Map, YourRace, OpponentRace, GameDate, ReplayFilePath, FileHash, SC2ClientVersion, Winner, CreatedAt, UpdatedAt) VALUES ('{replayGuid}', '{player1}', '{player2}', '{player1Id}', '{player2Id}', '{map}', '{race1}', '{race2}', '{gameDate:O}', '{replayFilePath}', '{fileHash}', '{sc2ClientVersion}', '{winner}', '{now}', '{now}')");

                return queryFactory.Query("Replays").InsertGetId<long>(insertData);
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error adding/updating replay: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Store build order entries for a replay.
        /// </summary>
        public void StoreBuildOrderEntries(long replayId, Queue<BuildOrderEntry> buildOrderEntries)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                // Clear existing entries
                queryFactory.Query("BuildOrderEntries")
                    .Where("ReplayId", replayId)
                    .Delete();

                // Insert new entries
                var entries = buildOrderEntries.Select(entry => new
                {
                    ReplayId = replayId,
                    PlayerId = entry.PlayerId,
                    TimeSeconds = entry.TimeSeconds,
                    Kind = entry.Kind,
                    Name = entry.Name
                }).ToList();

                if (entries.Any())
                {
                    queryFactory.Query("BuildOrderEntries").Insert(entries);
                }

                // Mark build order as cached
                queryFactory.Query("Replays")
                    .Where("Id", replayId)
                    .Update(new { BuildOrderCached = 1, CachedAt = DateTime.UtcNow.ToString("O") });

                // Console.WriteLine($"  ✓ Stored build order for replay ID {replayId}");
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error storing build order entries: {ex.Message}");
            }
        }

        /// <summary>
        /// Get replay record by file path.
        /// </summary>
        public ReplayRecord? GetReplayByFilePath(string filePath)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var query = queryFactory.Query("Replays")
                    .Where("ReplayFilePath", filePath);

                LogQuery(query, "SELECT (GetReplayByFilePath)");

                var replay = query.FirstOrDefault<dynamic>();

                return MapToReplayRecord(replay);
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving replay: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get build order entries for a replay.
        /// </summary>
        public Queue<BuildOrderEntry> GetBuildOrderEntries(long replayId)
        {
            var entries = new Queue<BuildOrderEntry>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var query = queryFactory.Query("BuildOrderEntries")
                    .Where("ReplayId", replayId)
                    .OrderBy("TimeSeconds");

                LogQuery(query, "SELECT (GetBuildOrderEntries)");

                var dbEntries = query.Get<dynamic>();

                foreach (var entry in dbEntries)
                {
                    entries.Enqueue(new BuildOrderEntry(
                        PlayerId: Convert.ToInt32(entry.PlayerId),
                        TimeSeconds: Convert.ToDouble(entry.TimeSeconds),
                        Kind: entry.Kind.ToString() ?? string.Empty,
                        Name: entry.Name.ToString() ?? string.Empty
                    ));
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving build order entries: {ex.Message}");
            }

            return entries;
        }

        /// <summary>
        /// Get replays by player name (either player1 or player2).
        /// </summary>
        public List<ReplayRecord> GetReplaysWithPlayer(string playerName)
        {
            var replays = new List<ReplayRecord>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var likePattern = $"%{playerName}%";
                var query = queryFactory.Query("Replays")
                    .Where(q => q.Where("You", "like", likePattern)
                                 .OrWhere("Opponent", "like", likePattern))
                    .OrderByDesc("GameDate");

                LogQuery(query, "SELECT (GetReplaysWithPlayer)");

                var dbReplays = query.Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    replays.Add(MapToReplayRecord(replay)!);
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving replays with player: {ex.Message}");
            }

            return replays;
        }

        /// <summary>
        /// Get match history against a specific opponent (in reverse chronological order).
        /// </summary>
        public List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName, string? winner, string replayFilePath)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10)
        {
            var history = new List<(string, DateTime, string, string, string, string, string?, string)>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var normalizedUserTag = NormalizeTag(yourPlayerName);
                var normalizedUserPrefix = GetTagPrefix(normalizedUserTag);
                var normalizedOppTag = NormalizeTag(opponentName);
                var normalizedOppPrefix = GetTagPrefix(normalizedOppTag);
                var oppLike = $"%{normalizedOppPrefix}%";

                System.Diagnostics.Debug.WriteLine(
                    $"[ReplayDatabase.GetOpponentMatchHistory] yourPlayerName={yourPlayerName}, opponentName={opponentName}, " +
                    $"limit={limit}, normalizedUser={normalizedUserTag}, normalizedOpp={normalizedOppTag}");

                var query = queryFactory.Query("Replays")
                    .Where(q => q
                        .Where("You", "like", oppLike)
                        .OrWhere("Opponent", "like", oppLike))
                    .OrderByDesc("GameDate");

                LogQuery(query, "SELECT (GetOpponentMatchHistory)");

                var dbReplays = query.Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    var player1 = replay.You?.ToString() ?? string.Empty;
                    var player2 = replay.Opponent?.ToString() ?? string.Empty;
                    var race1 = replay.YourRace?.ToString() ?? string.Empty;
                    var race2 = replay.OpponentRace?.ToString() ?? string.Empty;
                    var gameDate = DateTime.Parse(replay.GameDate?.ToString() ?? DateTime.MinValue.ToString("O"));
                    var map = replay.Map?.ToString() ?? string.Empty;
                    var replayPath = replay.ReplayFilePath?.ToString() ?? string.Empty;

                    var normP1 = NormalizeTag(player1);
                    var normP2 = NormalizeTag(player2);
                    var p1Prefix = GetTagPrefix(normP1);
                    var p2Prefix = GetTagPrefix(normP2);

                    bool p1IsUser = MatchesTagOrPrefix(normP1, p1Prefix, normalizedUserTag, normalizedUserPrefix);
                    bool p2IsUser = MatchesTagOrPrefix(normP2, p2Prefix, normalizedUserTag, normalizedUserPrefix);

                    bool p1IsOpp = MatchesTagOrPrefix(normP1, p1Prefix, normalizedOppTag, normalizedOppPrefix);
                    bool p2IsOpp = MatchesTagOrPrefix(normP2, p2Prefix, normalizedOppTag, normalizedOppPrefix);

                    string yourRace;
                    string opponentRaceInMatch;
                    string opponentInMatch;

                    if (p1IsUser && p2IsOpp)
                    {
                        yourRace = race1;
                        opponentRaceInMatch = race2;
                        opponentInMatch = player2;
                    }
                    else if (p2IsUser && p1IsOpp)
                    {
                        yourRace = race2;
                        opponentRaceInMatch = race1;
                        opponentInMatch = player1;
                    }
                    else
                    {
                        continue;
                    }

                    var replayFileName = Path.GetFileName(replayPath);
                    var winner = replay.Winner?.ToString();

                    System.Diagnostics.Debug.WriteLine(
                        $"[ReplayDatabase.GetOpponentMatchHistory] Match: you={yourRace} vs opponent={opponentRaceInMatch}, date={gameDate:yyyy-MM-dd}, map={map}, winner='{winner}'");

                    history.Add((opponentInMatch, gameDate, map, yourRace, opponentRaceInMatch, replayFileName, winner, replayPath));

                    if (history.Count >= limit)
                    {
                        break;
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetOpponentMatchHistory] Found {history.Count} matching replays");
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving opponent match history: {ex.Message}");
            }

            return history;
        }

        private static bool MatchesTagOrPrefix(string normalizedValue, string prefix, string targetTag, string targetPrefix)
        {
            if (string.IsNullOrEmpty(targetTag))
                return false;

            if (string.Equals(normalizedValue, targetTag, StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.IsNullOrEmpty(prefix) || string.IsNullOrEmpty(targetPrefix))
                return false;

            return string.Equals(prefix, targetPrefix, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Validates and normalizes a player name against known user accounts.
        /// Tries to find the best matching user account from the database.
        /// Returns the normalized battle tag if found, otherwise returns the input name normalized.
        /// </summary>
        public string ValidateAndNormalizePlayerName(string playerName)
        {
            return NormalizeTag(playerName);
        }

        /// <summary>
        /// Get all games against a specific opponent by their player ID.
        /// Returns games with player races to determine win/loss.
        /// Normalizes the toon handle by removing region prefix and searches using LIKE pattern.
        /// </summary>
        public List<(string, string, string, string, DateTime, string)>
            GetGamesByOpponentId(string yourPlayerId, string opponentPlayerId, int limit = 100)
        {
            var games = new List<(string, string, string, string, DateTime, string)>();

            // Normalize player IDs to handle both formats (with and without region prefix)
            // E.g., "1-S2-2-1369255" becomes "S2-2-1369255"
            string normalizedYourId = BuildOrderReader.NormalizeToonHandle(yourPlayerId);
            string normalizedOpponentId = BuildOrderReader.NormalizeToonHandle(opponentPlayerId);

            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] yourPlayerId={yourPlayerId}, normalized={normalizedYourId}");
            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] opponentPlayerId={opponentPlayerId}, normalized={normalizedOpponentId}");

            try
            {
                using var queryFactory = CreateQueryFactory();

                var query = queryFactory.Query("Replays")
                    .Where(q => q
                        .Where("YouId", "like", $"%{normalizedOpponentId}%")
                        .OrWhere("OpponentId", "like", $"%{normalizedOpponentId}%"))
                    .OrderByDesc("GameDate")
                    .Limit(limit);

                LogQuery(query, "SELECT (GetGamesByOpponentId)");

                var dbReplays = query.Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    var player1 = replay.You?.ToString() ?? string.Empty;
                    var player2 = replay.Opponent?.ToString() ?? string.Empty;
                    var race1 = replay.YourRace?.ToString() ?? string.Empty;
                    var race2 = replay.OpponentRace?.ToString() ?? string.Empty;
                    var gameDate = DateTime.Parse(replay.GameDate?.ToString() ?? DateTime.MinValue.ToString("O"));
                    var map = replay.Map?.ToString() ?? string.Empty;
                    var player1Id = replay.YouId?.ToString() ?? string.Empty;
                    var player2Id = replay.OpponentId?.ToString() ?? string.Empty;

                    // Normalize retrieved IDs for comparison
                    string normalizedPlayer1Id = BuildOrderReader.NormalizeToonHandle(player1Id);
                    string normalizedPlayer2Id = BuildOrderReader.NormalizeToonHandle(player2Id);

                    // Determine which player is the opponent (matching normalized ID)
                    string yourName, opponentName, yourRace, opponentRaceInMatch;

                    if (normalizedPlayer1Id.Equals(normalizedOpponentId, StringComparison.OrdinalIgnoreCase))
                    {
                        yourName = player2;
                        opponentName = player1;
                        yourRace = race2;
                        opponentRaceInMatch = race1;
                    }
                    else if (normalizedPlayer2Id.Equals(normalizedOpponentId, StringComparison.OrdinalIgnoreCase))
                    {
                        yourName = player1;
                        opponentName = player2;
                        yourRace = race1;
                        opponentRaceInMatch = race2;
                    }
                    else
                    {
                        // Skip if we can't determine which is opponent
                        continue;
                    }

                    games.Add((yourName, opponentName, yourRace, opponentRaceInMatch, gameDate, map));
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] Found {games.Count} games");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] Error: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// Get the most recent cached build order for a specific opponent.
        /// Normalizes the toon handle by removing region prefix and searches using LIKE pattern.
        /// </summary>
        public List<(double timeSeconds, string kind, string name)>? GetOpponentLastBuildOrder(string opponentName, int limit = 20)
        {
            try
            {
                // Search for build orders from replays where the opponent matches the given name
                // We search by opponent name/nickname from the lobby
                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetOpponentLastBuildOrder] Searching for opponent: {opponentName}");

                using var queryFactory = CreateQueryFactory();

                var dbEntries = queryFactory.Query("BuildOrderEntries as boe")
                    .Join("Replays as r", "boe.ReplayId", "r.Id")
                    .Where("r.Opponent", "=", opponentName)
                    .Where("r.BuildOrderCached", 1)
                    .OrderByDesc("r.GameDate")
                    .Limit(limit)
                    .Select("boe.TimeSeconds", "boe.Kind", "boe.Name")
                    .Get<dynamic>();

                var buildOrders = new List<(double, string, string)>();
                foreach (var entry in dbEntries)
                {
                    buildOrders.Add((
                        Convert.ToDouble(entry.TimeSeconds),
                        entry.Kind.ToString() ?? string.Empty,
                        entry.Name.ToString() ?? string.Empty
                    ));
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetOpponentLastBuildOrder] Found {buildOrders.Count} build order entries for opponent {opponentName}");

                return buildOrders.Count > 0 ? buildOrders : null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetOpponentLastBuildOrder] Error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get stored config metadata (hash, folder path, recursive flag, last validated timestamp).
        /// Returns null if no config has been stored yet.
        /// </summary>
        public (string hash, string folderPath, bool recursive, string lastValidated)? GetConfigMetadata()
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var record = queryFactory.Query("ConfigMetadata")
                    .Where("Id", 1)
                    .FirstOrDefault<dynamic>();

                if (record != null)
                {
                    return (
                        record.ConfigHash?.ToString() ?? string.Empty,
                        record.ReplayFolderPath?.ToString() ?? string.Empty,
                        Convert.ToInt32(record.RecursiveScan ?? 0) == 1,
                        record.LastValidated?.ToString() ?? DateTime.UtcNow.ToString("O")
                    );
                }

                return null;
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving config metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Store or update config metadata (hash, folder path, recursive flag).
        /// Replaces existing metadata to maintain singleton constraint.
        /// </summary>
        public void UpdateConfigMetadata(string configHash, string folderPath, bool recursive)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                const string sql = @"
                    INSERT OR REPLACE INTO ConfigMetadata (Id, ConfigHash, ReplayFolderPath, RecursiveScan, LastValidated)
                    VALUES (1, @ConfigHash, @ReplayFolderPath, @RecursiveScan, @LastValidated)";

                queryFactory.Statement(sql, new
                {
                    ConfigHash = configHash,
                    ReplayFolderPath = folderPath,
                    RecursiveScan = recursive ? 1 : 0,
                    LastValidated = DateTime.UtcNow.ToString("O")
                });
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error updating config metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all cached replay records (minimal data - just ID and filepath for cache management).
        /// Used by CacheManager to load all cached files into memory for efficient diff operations.
        /// </summary>
        public List<ReplayRecord> GetAllCachedReplays()
        {
            var replays = new List<ReplayRecord>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var dbReplays = queryFactory.Query("Replays")
                    .Select("Id", "ReplayFilePath")
                    .OrderByDesc("CreatedAt")
                    .Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    replays.Add(new ReplayRecord
                    {
                        Id = Convert.ToInt64(replay.Id),
                        ReplayFilePath = replay.ReplayFilePath?.ToString() ?? string.Empty
                    });
                }
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving all cached replays: {ex.Message}");
            }

            return replays;
        }

        /// <summary>
        /// Get list of replay files from disk that are NOT in the database.
        /// Returns files that need to be added to cache.
        /// </summary>
        public List<string> GetMissingReplayFiles(string[] allReplayFilesOnDisk)
        {
            var missingFiles = new List<string>();

            if (allReplayFilesOnDisk.Length == 0)
                return missingFiles;

            try
            {
                using var queryFactory = CreateQueryFactory();

                // Get all cached file paths
                var cachedPaths = new HashSet<string>(
                    queryFactory.Query("Replays").Select("ReplayFilePath").Get<string>().Where(p => !string.IsNullOrEmpty(p)),
                    StringComparer.OrdinalIgnoreCase);

                // Find missing files (on disk but not in cache)
                foreach (var filePath in allReplayFilesOnDisk)
                {
                    if (!cachedPaths.Contains(filePath))
                    {
                        missingFiles.Add(filePath);
                    }
                }
            }
            catch
            {
                // Fallback: return all files if query fails (safer to re-process than skip)
                return new List<string>(allReplayFilesOnDisk);
            }

            return missingFiles;
        }

        /// <summary>
        /// Get database statistics.
        /// </summary>
        public (int Total, int WithBuildOrder) GetDatabaseStats()
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var total = queryFactory.Query("Replays").Count<long>();
                var withBuildOrder = queryFactory.Query("Replays")
                    .Where("BuildOrderCached", 1)
                    .Count<long>();

                return ((int)total, (int)withBuildOrder);
            }
            catch
            {
                return (0, 0);
            }
        }

        /// <summary>
        /// Cache replay metadata (quick insert without full replay processing).
        /// Safe to call multiple times - automatically skips duplicates by file path.
        /// Used during initial cache build on first startup.
        /// </summary>
        public void CacheMetadata(ReplayMetadata metadata, string? userBattleTag = null)
        {
            try
            {
                // If we already have this replay by file path, skip it (idempotent)
                var existingReplay = GetReplayByFilePath(metadata.FilePath);
                if (existingReplay != null)
                {
                    return;
                }

                // Persist opponents (skip current user if we can identify them)
                UpsertOpponents(metadata.Players, userBattleTag);

                // Extract player data
                string player1 = string.Empty, player2 = string.Empty;
                string race1 = string.Empty, race2 = string.Empty;
                string? player1Id = null, player2Id = null;

                if (metadata.Players.Count > 0)
                {
                    player1 = metadata.Players[0].Name;
                    race1 = metadata.Players[0].Race;
                    player1Id = metadata.Players[0].PlayerId;
                }

                if (metadata.Players.Count > 1)
                {
                    player2 = metadata.Players[1].Name;
                    race2 = metadata.Players[1].Race;
                    player2Id = metadata.Players[1].PlayerId;
                }

                // Use map from metadata, fallback to "Unknown"
                var map = !string.IsNullOrEmpty(metadata.Map) ? metadata.Map : "Unknown";
                var gameDate = metadata.GameDate;
                var sc2Version = metadata.SC2ClientVersion;

                AddOrUpdateReplay(player1, player2, map, race1, race2, gameDate, metadata.FilePath, sc2Version, player1Id, player2Id);
                // Progress is displayed by the caller (RevealTool)
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error caching metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Get cache statistics (alias for database stats for compatibility).
        /// </summary>
        public (int Total, int WithPlayers) GetCacheStats()
        {
            var (total, withBuildOrder) = GetDatabaseStats();
            return (total, withBuildOrder);
        }

        private ReplayRecord? MapToReplayRecord(dynamic? replay)
        {
            if (replay == null)
                return null;

            return new ReplayRecord
            {
                Id = Convert.ToInt64(replay.Id),
                Player1 = replay.You?.ToString() ?? string.Empty,
                Player2 = replay.Opponent?.ToString() ?? string.Empty,
                Player1Id = replay.YouId?.ToString(),
                Player2Id = replay.OpponentId?.ToString(),
                Map = replay.Map?.ToString() ?? string.Empty,
                Race1 = replay.YourRace?.ToString() ?? string.Empty,
                Race2 = replay.OpponentRace?.ToString() ?? string.Empty,
                GameDate = DateTime.Parse(replay.GameDate?.ToString() ?? DateTime.MinValue.ToString("O")),
                ReplayFilePath = replay.ReplayFilePath?.ToString() ?? string.Empty,
                BuildOrderCached = Convert.ToInt32(replay.BuildOrderCached ?? 0) == 1,
                CachedAt = replay.CachedAt != null && replay.CachedAt != DBNull.Value
                    ? DateTime.Parse(replay.CachedAt.ToString())
                    : null
            };
        }

        /// <summary>
        /// Upsert opponent records from replay players, skipping the detected user.
        /// </summary>
        /// <param name="players">Replay players to store.</param>
        /// <param name="userBattleTag">Detected user battle tag (with _ or #).</param>
        public void UpsertOpponents(IEnumerable<PlayerInfo>? players, string? userBattleTag = null)
        {
            if (players == null)
                return;

            var playerList = players.Where(p => p != null).ToList();
            if (playerList.Count == 0)
                return;

            string normalizedUserTag = NormalizeTag(userBattleTag);
            string userPrefix = GetTagPrefix(normalizedUserTag);
            var now = DateTime.UtcNow.ToString("O");

            using var queryFactory = CreateQueryFactory();
            const string upsertSql = @"
                    INSERT INTO Opponents (Name, DisplayName, NickName, ToonHandle, BattleTag, NormalizedBattleTag, FirstSeen, LastSeen, UpdatedAt)
                    VALUES (@Name, @DisplayName, @NickName, @ToonHandle, @BattleTag, @NormalizedBattleTag, @FirstSeen, @LastSeen, @UpdatedAt)
                    ON CONFLICT(Name) DO UPDATE SET
                        DisplayName = excluded.DisplayName,
                        NickName = excluded.NickName,
                        ToonHandle = CASE WHEN excluded.ToonHandle != '' THEN excluded.ToonHandle ELSE Opponents.ToonHandle END,
                        BattleTag = CASE WHEN excluded.BattleTag != '' THEN excluded.BattleTag ELSE Opponents.BattleTag END,
                        NormalizedBattleTag = CASE WHEN excluded.NormalizedBattleTag != '' THEN excluded.NormalizedBattleTag ELSE Opponents.NormalizedBattleTag END,
                        LastSeen = excluded.LastSeen,
                        UpdatedAt = excluded.UpdatedAt;
                ";

            foreach (var player in playerList)
            {
                string displayName = BuildDisplayName(player);
                string normalizedName = NormalizeTag(displayName);
                if (string.IsNullOrWhiteSpace(normalizedName))
                    continue;

                string battleTag = BuildBattleTag(player);
                if (string.IsNullOrWhiteSpace(battleTag))
                    continue;

                if (MatchesConfiguredUserAccount(normalizedName, battleTag, normalizedUserTag, userPrefix))
                    continue;

                string toonHandle = string.IsNullOrWhiteSpace(player.PlayerId)
                    ? string.Empty
                    : BuildOrderReader.NormalizeToonHandle(player.PlayerId);

                var parameters = new
                {
                    Name = normalizedName,
                    DisplayName = displayName,
                    NickName = displayName,
                    ToonHandle = string.IsNullOrWhiteSpace(toonHandle) ? null : toonHandle,
                    BattleTag = battleTag,
                    NormalizedBattleTag = NormalizeTag(battleTag),
                    FirstSeen = now,
                    LastSeen = now,
                    UpdatedAt = now
                };

                try
                {
                    queryFactory.Statement(upsertSql, parameters);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Failed to upsert opponent {normalizedName}: {ex.Message}");
                }
            }
        }

        private static bool MatchesConfiguredUserAccount(string normalizedName, string normalizedBattleTag,
            string normalizedUserTag, string normalizedUserPrefix)
        {
            if (string.IsNullOrEmpty(normalizedUserTag))
                return false;

            if (string.Equals(normalizedName, normalizedUserTag, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedBattleTag, normalizedUserTag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.IsNullOrEmpty(normalizedUserPrefix))
                return false;

            var namePrefix = GetTagPrefix(normalizedName);
            if (!string.IsNullOrEmpty(namePrefix) &&
                string.Equals(namePrefix, normalizedUserPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var battlePrefix = GetTagPrefix(normalizedBattleTag);
            if (!string.IsNullOrEmpty(battlePrefix) &&
                string.Equals(battlePrefix, normalizedUserPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static string BuildDisplayName(PlayerInfo player)
        {
            var normalizedName = NormalizeTag(player.Name);
            if (HasBattleTagSuffix(normalizedName))
            {
                return normalizedName;
            }

            var normalizedBattleTag = NormalizeTag(player.BattleTag);
            if (HasBattleTagSuffix(normalizedBattleTag))
            {
                return normalizedBattleTag;
            }

            return !string.IsNullOrEmpty(normalizedName)
                ? normalizedName
                : normalizedBattleTag;
        }

        private static string BuildBattleTag(PlayerInfo player)
        {
            var normalizedBattleTag = NormalizeTag(player.BattleTag);
            if (HasBattleTagSuffix(normalizedBattleTag))
            {
                return normalizedBattleTag;
            }

            var normalizedName = NormalizeTag(player.Name);
            if (HasBattleTagSuffix(normalizedName))
            {
                return normalizedName;
            }

            return !string.IsNullOrEmpty(normalizedBattleTag)
                ? normalizedBattleTag
                : normalizedName;
        }

        private static bool HasBattleTagSuffix(string? value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Contains('#');
        }

        private static string NormalizeTag(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace('_', '#').Trim();
        }

        private static string GetTagPrefix(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            var hashIndex = value.IndexOf('#');
            return hashIndex > 0 ? value.Substring(0, hashIndex) : value;
        }

        private static string ComputeFileHash(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var hash = $"{fileInfo.Length}";

                using var file = File.OpenRead(filePath);
                byte[] buffer = new byte[1024];

                file.Read(buffer, 0, 1024);
                hash += BitConverter.ToString(buffer, 0, Math.Min(32, buffer.Length));

                if (file.Length > 2048)
                {
                    file.Seek(-1024, SeekOrigin.End);
                    file.Read(buffer, 0, 1024);
                    hash += BitConverter.ToString(buffer, 0, Math.Min(32, buffer.Length));
                }

                return hash;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>
    /// Represents a replay record from the database.
    /// </summary>
    public class ReplayRecord
    {
        public long Id { get; set; }
        public string Player1 { get; set; } = string.Empty;
        public string Player2 { get; set; } = string.Empty;
        public string? Player1Id { get; set; }
        public string? Player2Id { get; set; }
        public string Map { get; set; } = string.Empty;
        public string Race1 { get; set; } = string.Empty;
        public string Race2 { get; set; } = string.Empty;
        public DateTime GameDate { get; set; }
        public string ReplayFilePath { get; set; } = string.Empty;
        public bool BuildOrderCached { get; set; }
        public DateTime? CachedAt { get; set; }
    }
}
