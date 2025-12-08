using System.Data.SQLite;
using System.Reflection;
using System.Linq;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using BarcodeRevealTool.Engine.Config;

namespace BarcodeRevealTool.Replay
{
    /// <summary>
    /// Main database for storing and retrieving replay information and build orders.
    /// </summary>
    public class ReplayDatabase
    {
        private readonly string _databasePath;
        private const string DatabaseFileName = "cache.db";
        private bool _isRefreshing = false; // Guard against re-entrant calls to RefreshUserAccounts
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

            // Populate UserAccounts table on first startup
            PopulateUserAccounts();
        }

        /// <summary>
        /// Populate UserAccounts table with all discovered toon handles from account folder structure.
        /// This is called once on first database initialization.
        /// </summary>
        public void PopulateUserAccounts()
        {
            using var queryFactory = CreateQueryFactory();

            // Discover all toon handles and their nick mappings
            var toonHandles = Engine.Config.AccountToonDiscoveryService.DiscoverAllToonHandles();
            var nickMapping = Engine.Config.AccountToonDiscoveryService.DiscoverToonNickMapping();

            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Populating UserAccounts with {toonHandles.Count} discovered accounts");

            const string insertSql = @"
                    INSERT OR IGNORE INTO UserAccounts 
                    (ToonHandle, NickName, BattleTag, Region, Realm, BattleNetId, DiscoveredAt, UpdatedAt)
                    VALUES (@ToonHandle, @NickName, @BattleTag, @Region, @Realm, @BattleNetId, @DiscoveredAt, @UpdatedAt)";

            foreach (var toon in toonHandles)
            {
                var region = Engine.Config.AccountToonDiscoveryService.ExtractRegion(toon);
                var realm = Engine.Config.AccountToonDiscoveryService.ExtractRealm(toon);
                var battleNetId = Engine.Config.AccountToonDiscoveryService.ExtractBattleNetId(toon);

                // Get nick name and discriminator from mapping, or construct battle tag
                string? nickName = null;
                string battleTag;
                if (nickMapping.ContainsKey(toon))
                {
                    var (nick, discriminator) = nickMapping[toon];
                    nickName = nick;
                    battleTag = $"{nick}#{discriminator}";
                }
                else
                {
                    // Fallback if nick mapping not found
                    battleTag = $"Player_{battleNetId}";
                }

                var parameters = new
                {
                    ToonHandle = toon,
                    NickName = nickName,
                    BattleTag = battleTag,
                    Region = region ?? "0",
                    Realm = realm ?? "0",
                    BattleNetId = battleNetId ?? "0",
                    DiscoveredAt = DateTime.UtcNow.ToString("O"),
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                };

                try
                {
                    queryFactory.Statement(insertSql, parameters);
                    System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Added UserAccount: {toon} -> {battleTag}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Error adding UserAccount {toon}: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Finished populating UserAccounts");
        }

        /// <summary>
        /// Refresh the UserAccounts table by checking for new accounts and adding them.
        /// Does NOT delete existing accounts, only adds new ones discovered.
        /// Call this after cache initialization to ensure accounts are up-to-date.
        /// </summary>
        public void RefreshUserAccounts()
        {
            // Guard against re-entrant calls (watcher detecting changes during discovery)
            if (_isRefreshing)
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] RefreshUserAccounts already in progress, skipping re-entrant call");
                return;
            }

            _isRefreshing = true;
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Refreshing UserAccounts table (checking for NEW accounts only)");

                using var queryFactory = CreateQueryFactory();

                // Get currently known toon handles from database
                var knownHandles = new HashSet<string>(
                    queryFactory.Query("UserAccounts").Select("ToonHandle").Get<string>(),
                    StringComparer.OrdinalIgnoreCase);

                // Discover all toon handles from file system
                var allToonHandles = Engine.Config.AccountToonDiscoveryService.DiscoverAllToonHandles();
                var nickMapping = Engine.Config.AccountToonDiscoveryService.DiscoverToonNickMapping();

                // Only add NEW toons that aren't already in database
                var newToons = allToonHandles.Where(t => !knownHandles.Contains(t)).ToList();

                if (newToons.Count == 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] No new accounts found");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Found {newToons.Count} new accounts to add");

                foreach (var toon in newToons)
                {
                    var region = Engine.Config.AccountToonDiscoveryService.ExtractRegion(toon);
                    var realm = Engine.Config.AccountToonDiscoveryService.ExtractRealm(toon);
                    var battleNetId = Engine.Config.AccountToonDiscoveryService.ExtractBattleNetId(toon);

                    // Get nick name and discriminator from mapping, or construct battle tag
                    string? nickName = null;
                    string battleTag;
                    if (nickMapping.ContainsKey(toon))
                    {
                        var (nick, discriminator) = nickMapping[toon];
                        nickName = nick;
                        battleTag = $"{nick}#{discriminator}";
                    }
                    else
                    {
                        // Fallback if nick mapping not found
                        battleTag = $"Player_{battleNetId}";
                    }

                    try
                    {
                        queryFactory.Query("UserAccounts").Insert(new
                        {
                            ToonHandle = toon,
                            NickName = nickName,
                            BattleTag = battleTag,
                            Region = region ?? "0",
                            Realm = realm ?? "0",
                            BattleNetId = battleNetId ?? "0",
                            DiscoveredAt = DateTime.UtcNow.ToString("O"),
                            UpdatedAt = DateTime.UtcNow.ToString("O")
                        });
                        System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Added NEW UserAccount: {toon} -> {battleTag}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Error adding UserAccount {toon}: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Finished refreshing UserAccounts ({newToons.Count} new accounts added)");
            }
            finally
            {
                _isRefreshing = false;
            }
        }

        /// <summary>
        /// Add or update a replay record in the database (INSERT ONLY - never deletes).
        /// </summary>
        public long AddOrUpdateReplay(string player1, string player2, string map, string race1, string race2,
            DateTime gameDate, string replayFilePath, string? sc2ClientVersion = null, string? player1Id = null, string? player2Id = null)
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

                return queryFactory.Query("Replays").InsertGetId<long>(new
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
                    CreatedAt = now,
                    UpdatedAt = now
                });
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

                var replay = queryFactory.Query("Replays")
                    .Where("ReplayFilePath", filePath)
                    .FirstOrDefault<dynamic>();

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

                var dbEntries = queryFactory.Query("BuildOrderEntries")
                    .Where("ReplayId", replayId)
                    .OrderBy("TimeSeconds")
                    .Get<dynamic>();

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
                var dbReplays = queryFactory.Query("Replays")
                    .Where(q => q.Where("You", "like", likePattern)
                                 .OrWhere("Opponent", "like", likePattern))
                    .OrderByDesc("GameDate")
                    .Get<dynamic>();

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
        public List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10)
        {
            var history = new List<(string, DateTime, string, string, string, string)>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                // Load all known user nicknames from UserAccounts (local user's accounts)
                // "Nickname" here means full tag: Name#123. We also derive name-only prefixes for matching
                // against Replays.You/Opponent, which may only contain the name part.
                var userRows = queryFactory.Query("UserAccounts")
                    .Select("NickName", "BattleTag")
                    .Get<dynamic>();

                var userFullTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var userNamePrefixes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in userRows)
                {
                    string rawNick = row.NickName?.ToString() ?? string.Empty;
                    string rawBattleTag = row.BattleTag?.ToString() ?? string.Empty;

                    string normalized = !string.IsNullOrWhiteSpace(rawNick)
                        ? NormalizeTag(rawNick)
                        : NormalizeTag(rawBattleTag);

                    if (!string.IsNullOrWhiteSpace(normalized))
                    {
                        userFullTags.Add(normalized); // e.g., "Ignacy#236"
                        var prefix = GetTagPrefix(normalized); // e.g., "Ignacy"
                        if (!string.IsNullOrWhiteSpace(prefix))
                        {
                            userNamePrefixes.Add(prefix);
                        }
                    }
                }

                var normalizedOppTag = NormalizeTag(opponentName);          // "Ignacy#236"
                var normalizedOppNick = GetTagPrefix(normalizedOppTag);     // "Ignacy"
                var oppLike = $"%{normalizedOppNick}%";                      // DB stores names, not full tags

                System.Diagnostics.Debug.WriteLine(
                    $"[ReplayDatabase.GetOpponentMatchHistory] yourPlayerName={yourPlayerName}, opponentName={opponentName}, " +
                    $"limit={limit}, userNicknames=[{string.Join(", ", userFullTags)}], oppLike={oppLike}, normalizedOppTag={normalizedOppTag}, normalizedOppNick={normalizedOppNick}");

                var dbReplays = queryFactory.Query("Replays")
                    .Where(q => q
                        .Where("You", "like", oppLike)
                        .OrWhere("Opponent", "like", oppLike))
                    .OrderByDesc("GameDate")
                    .Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    var player1 = replay.You?.ToString() ?? string.Empty;
                    var player2 = replay.Opponent?.ToString() ?? string.Empty;
                    var race1 = replay.YourRace?.ToString() ?? string.Empty;
                    var race2 = replay.OpponentRace?.ToString() ?? string.Empty;
                    var gameDate = DateTime.Parse(replay.GameDate?.ToString() ?? DateTime.MinValue.ToString("O"));
                    var map = replay.Map?.ToString() ?? string.Empty;
                    var replayPath = replay.ReplayFilePath?.ToString() ?? string.Empty;

                    // Normalize player names and derive prefixes to match both full tags and plain names
                    var normP1 = NormalizeTag(player1);
                    var normP2 = NormalizeTag(player2);
                    var p1Prefix = GetTagPrefix(normP1);
                    var p2Prefix = GetTagPrefix(normP2);

                    bool p1IsUser = userFullTags.Contains(normP1) || userNamePrefixes.Contains(p1Prefix);
                    bool p2IsUser = userFullTags.Contains(normP2) || userNamePrefixes.Contains(p2Prefix);

                    bool p1IsOpp = normP1.Equals(normalizedOppTag, StringComparison.OrdinalIgnoreCase) ||
                                   p1Prefix.Equals(normalizedOppNick, StringComparison.OrdinalIgnoreCase);
                    bool p2IsOpp = normP2.Equals(normalizedOppTag, StringComparison.OrdinalIgnoreCase) ||
                                   p2Prefix.Equals(normalizedOppNick, StringComparison.OrdinalIgnoreCase);

                    // Determine which player is "you" (any of user's accounts) and which is opponent
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
                        // Skip replays where we can't clearly identify user vs opponent
                        continue;
                    }

                    var replayFileName = Path.GetFileName(replayPath);

                    history.Add((opponentInMatch, gameDate, map, yourRace, opponentRaceInMatch, replayFileName));

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

            // Extract the battle.net ID (last part) for LIKE queries
            // E.g., "S2-2-1369255" → last part is "1369255"
            string? opponentIdLastBit = normalizedOpponentId.Split('-').LastOrDefault();
            string? yourIdLastBit = normalizedYourId.Split('-').LastOrDefault();

            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] yourPlayerId={yourPlayerId}, normalized={normalizedYourId}");
            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] opponentPlayerId={opponentPlayerId}, normalized={normalizedOpponentId}, idBit={opponentIdLastBit}");

            try
            {
                using var queryFactory = CreateQueryFactory();

                var dbReplays = queryFactory.Query("Replays")
                    .Where(q => q
                        .Where("YouId", "like", $"%{opponentIdLastBit}%")
                        .OrWhere("OpponentId", "like", $"%{opponentIdLastBit}%"))
                    .OrderByDesc("GameDate")
                    .Limit(limit)
                    .Get<dynamic>();

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

                    if (normalizedPlayer1Id.EndsWith(opponentIdLastBit ?? "", StringComparison.OrdinalIgnoreCase))
                    {
                        yourName = player2;
                        opponentName = player1;
                        yourRace = race2;
                        opponentRaceInMatch = race1;
                    }
                    else if (normalizedPlayer2Id.EndsWith(opponentIdLastBit ?? "", StringComparison.OrdinalIgnoreCase))
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
        public List<(double timeSeconds, string kind, string name)>? GetOpponentLastBuildOrder(string opponentPlayerId, int limit = 20)
        {
            try
            {
                // Normalize player ID to handle both formats (with and without region prefix)
                // E.g., "1-S2-2-1369255" becomes "S2-2-1369255"
                string normalizedOpponentId = BuildOrderReader.NormalizeToonHandle(opponentPlayerId);

                // Extract just the battle.net ID (last part) for LIKE queries
                // E.g., "S2-2-1369255" → last part is "1369255"
                string? opponentIdLastBit = normalizedOpponentId.Split('-').LastOrDefault();

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetOpponentLastBuildOrder] opponentPlayerId={opponentPlayerId}, normalized={normalizedOpponentId}, idBit={opponentIdLastBit}");

                using var queryFactory = CreateQueryFactory();

                var dbEntries = queryFactory.Query("BuildOrderEntries as boe")
                    .Join("Replays as r", "boe.ReplayId", "r.Id")
                    .Where(q => q.Where("r.Player1Id", "like", $"%{opponentIdLastBit}%")
                                 .OrWhere("r.Player2Id", "like", $"%{opponentIdLastBit}%"))
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

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetOpponentLastBuildOrder] Found {buildOrders.Count} build order entries");

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
                    INSERT INTO Opponents (Name, ToonHandle, BattleTag, FirstSeen, LastSeen, UpdatedAt)
                    VALUES (@Name, @ToonHandle, @BattleTag, @FirstSeen, @LastSeen, @UpdatedAt)
                    ON CONFLICT(Name) DO UPDATE SET
                        ToonHandle = CASE WHEN excluded.ToonHandle != '' THEN excluded.ToonHandle ELSE Opponents.ToonHandle END,
                        BattleTag = CASE WHEN excluded.BattleTag != '' THEN excluded.BattleTag ELSE Opponents.BattleTag END,
                        LastSeen = excluded.LastSeen,
                        UpdatedAt = excluded.UpdatedAt;
                ";

            foreach (var player in playerList)
            {
                string name = NormalizeTag(player.Name);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                string battleTag = string.IsNullOrWhiteSpace(player.BattleTag)
                    ? InferBattleTag(name)
                    : NormalizeTag(player.BattleTag);

                // Skip storing the current user as an opponent
                if (!string.IsNullOrEmpty(normalizedUserTag))
                {
                    string namePrefix = GetTagPrefix(name);
                    string battlePrefix = GetTagPrefix(battleTag);

                    if (name.Equals(normalizedUserTag, StringComparison.OrdinalIgnoreCase) ||
                        battleTag.Equals(normalizedUserTag, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(userPrefix) && (namePrefix.Equals(userPrefix, StringComparison.OrdinalIgnoreCase) ||
                                                               battlePrefix.Equals(userPrefix, StringComparison.OrdinalIgnoreCase))))
                    {
                        continue;
                    }
                }

                string toonHandle = string.IsNullOrWhiteSpace(player.PlayerId)
                    ? string.Empty
                    : BuildOrderReader.NormalizeToonHandle(player.PlayerId);

                var parameters = new
                {
                    Name = name,
                    ToonHandle = string.IsNullOrWhiteSpace(toonHandle) ? null : toonHandle,
                    BattleTag = string.IsNullOrWhiteSpace(battleTag) ? null : battleTag,
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
                    System.Diagnostics.Debug.WriteLine($"[ReplayDatabase] Failed to upsert opponent {name}: {ex.Message}");
                }
            }
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

        private static string InferBattleTag(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            // Prefer display format with '#'
            return NormalizeTag(name);
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
