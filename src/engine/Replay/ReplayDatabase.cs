using System.Data.SQLite;
using System.Reflection;

namespace BarcodeRevealTool.Replay
{
    /// <summary>
    /// Main database for storing and retrieving replay information and build orders.
    /// </summary>
    public class ReplayDatabase
    {
        private readonly string _databasePath;
        private const string DatabaseFileName = "cache.db";

        public ReplayDatabase(string? customPath = null)
        {
            var dbPath = customPath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "_db"
            );

            Directory.CreateDirectory(dbPath);
            _databasePath = Path.Combine(dbPath, DatabaseFileName);

            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
            connection.Open();

            using var command = connection.CreateCommand();
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
            command.CommandText = schemaSql;
            command.ExecuteNonQuery();
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

                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                // First, try to get existing replay by GUID
                using var selectCommand = connection.CreateCommand();
                selectCommand.CommandText = "SELECT Id FROM Replays WHERE ReplayGuid = @ReplayGuid";
                selectCommand.Parameters.AddWithValue("@ReplayGuid", replayGuid.ToString());

                var existingId = selectCommand.ExecuteScalar();
                if (existingId != null)
                {
                    // Replay already exists, return its ID without modifying
                    return Convert.ToInt64(existingId);
                }

                // New replay - insert only
                var fileHash = ComputeFileHash(replayFilePath);
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO Replays
                    (ReplayGuid, Player1, Player2, Player1Id, Player2Id, Map, Race1, Race2, GameDate, ReplayFilePath, FileHash, SC2ClientVersion, CreatedAt, UpdatedAt)
                    VALUES (@ReplayGuid, @Player1, @Player2, @Player1Id, @Player2Id, @Map, @Race1, @Race2, @GameDate, @ReplayFilePath, @FileHash, @SC2ClientVersion, @CreatedAt, @UpdatedAt);
                    SELECT last_insert_rowid();
                ";
                insertCommand.Parameters.AddWithValue("@ReplayGuid", replayGuid.ToString());
                insertCommand.Parameters.AddWithValue("@Player1", player1);
                insertCommand.Parameters.AddWithValue("@Player2", player2);
                insertCommand.Parameters.AddWithValue("@Player1Id", player1Id ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@Player2Id", player2Id ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@Map", map);
                insertCommand.Parameters.AddWithValue("@Race1", race1);
                insertCommand.Parameters.AddWithValue("@Race2", race2);
                insertCommand.Parameters.AddWithValue("@GameDate", gameDate.ToString("O"));
                insertCommand.Parameters.AddWithValue("@ReplayFilePath", replayFilePath);
                insertCommand.Parameters.AddWithValue("@FileHash", fileHash);
                insertCommand.Parameters.AddWithValue("@SC2ClientVersion", sc2ClientVersion ?? (object)DBNull.Value);
                insertCommand.Parameters.AddWithValue("@CreatedAt", DateTime.UtcNow.ToString("O"));
                insertCommand.Parameters.AddWithValue("@UpdatedAt", DateTime.UtcNow.ToString("O"));

                var result = insertCommand.ExecuteScalar();
                return result != null ? Convert.ToInt64(result) : 0;
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                // Clear existing entries
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM BuildOrderEntries WHERE ReplayId = @ReplayId";
                    command.Parameters.AddWithValue("@ReplayId", replayId);
                    command.ExecuteNonQuery();
                }

                // Insert new entries
                using var insertCommand = connection.CreateCommand();
                insertCommand.CommandText = @"
                    INSERT INTO BuildOrderEntries (ReplayId, PlayerId, TimeSeconds, Kind, Name)
                    VALUES (@ReplayId, @PlayerId, @TimeSeconds, @Kind, @Name)
                ";

                foreach (var entry in buildOrderEntries)
                {
                    insertCommand.Parameters.Clear();
                    insertCommand.Parameters.AddWithValue("@ReplayId", replayId);
                    insertCommand.Parameters.AddWithValue("@PlayerId", entry.PlayerId);
                    insertCommand.Parameters.AddWithValue("@TimeSeconds", entry.TimeSeconds);
                    insertCommand.Parameters.AddWithValue("@Kind", entry.Kind);
                    insertCommand.Parameters.AddWithValue("@Name", entry.Name);
                    insertCommand.ExecuteNonQuery();
                }

                // Mark build order as cached
                using var updateCommand = connection.CreateCommand();
                updateCommand.CommandText = @"
                    UPDATE Replays
                    SET BuildOrderCached = 1, CachedAt = @CachedAt
                    WHERE Id = @ReplayId
                ";
                updateCommand.Parameters.AddWithValue("@ReplayId", replayId);
                updateCommand.Parameters.AddWithValue("@CachedAt", DateTime.UtcNow.ToString("O"));
                updateCommand.ExecuteNonQuery();

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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Player1, Player2, Map, Race1, Race2, GameDate, ReplayFilePath, BuildOrderCached, CachedAt
                    FROM Replays
                    WHERE ReplayFilePath = @ReplayFilePath
                ";
                command.Parameters.AddWithValue("@ReplayFilePath", filePath);

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new ReplayRecord
                    {
                        Id = Convert.ToInt64(reader["Id"]),
                        Player1 = reader["Player1"].ToString() ?? string.Empty,
                        Player2 = reader["Player2"].ToString() ?? string.Empty,
                        Map = reader["Map"].ToString() ?? string.Empty,
                        Race1 = reader["Race1"].ToString() ?? string.Empty,
                        Race2 = reader["Race2"].ToString() ?? string.Empty,
                        GameDate = DateTime.Parse(reader["GameDate"].ToString() ?? DateTime.MinValue.ToString("O")),
                        ReplayFilePath = reader["ReplayFilePath"].ToString() ?? string.Empty,
                        BuildOrderCached = Convert.ToInt32(reader["BuildOrderCached"]) == 1,
                        CachedAt = reader["CachedAt"] != DBNull.Value ? DateTime.Parse(reader["CachedAt"].ToString() ?? DateTime.MinValue.ToString("O")) : null
                    };
                }

                return null;
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT PlayerId, TimeSeconds, Kind, Name
                    FROM BuildOrderEntries
                    WHERE ReplayId = @ReplayId
                    ORDER BY TimeSeconds ASC
                ";
                command.Parameters.AddWithValue("@ReplayId", replayId);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    entries.Enqueue(new BuildOrderEntry(
                        PlayerId: Convert.ToInt32(reader["PlayerId"]),
                        TimeSeconds: Convert.ToDouble(reader["TimeSeconds"]),
                        Kind: reader["Kind"].ToString() ?? string.Empty,
                        Name: reader["Name"].ToString() ?? string.Empty
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, Player1, Player2, Map, Race1, Race2, GameDate, ReplayFilePath, BuildOrderCached, CachedAt
                    FROM Replays
                    WHERE Player1 LIKE @PlayerName OR Player2 LIKE @PlayerName
                    ORDER BY GameDate DESC
                ";
                command.Parameters.AddWithValue("@PlayerName", $"%{playerName}%");

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    replays.Add(new ReplayRecord
                    {
                        Id = Convert.ToInt64(reader["Id"]),
                        Player1 = reader["Player1"].ToString() ?? string.Empty,
                        Player2 = reader["Player2"].ToString() ?? string.Empty,
                        Map = reader["Map"].ToString() ?? string.Empty,
                        Race1 = reader["Race1"].ToString() ?? string.Empty,
                        Race2 = reader["Race2"].ToString() ?? string.Empty,
                        GameDate = DateTime.Parse(reader["GameDate"].ToString() ?? DateTime.MinValue.ToString("O")),
                        ReplayFilePath = reader["ReplayFilePath"].ToString() ?? string.Empty,
                        BuildOrderCached = Convert.ToInt32(reader["BuildOrderCached"]) == 1,
                        CachedAt = reader["CachedAt"] != DBNull.Value ? DateTime.Parse(reader["CachedAt"].ToString() ?? DateTime.MinValue.ToString("O")) : null
                    });
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Player1, Player2, Map, Race1, Race2, GameDate, ReplayFilePath
                    FROM Replays
                    WHERE (Player1 LIKE @YourName AND Player2 LIKE @OpponentName)
                       OR (Player2 LIKE @YourName AND Player1 LIKE @OpponentName)
                    ORDER BY GameDate DESC
                    LIMIT @Limit
                ";
                command.Parameters.AddWithValue("@YourName", $"%{yourPlayerName}%");
                command.Parameters.AddWithValue("@OpponentName", $"%{opponentName}%");
                command.Parameters.AddWithValue("@Limit", limit);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var player1 = reader["Player1"].ToString() ?? string.Empty;
                    var player2 = reader["Player2"].ToString() ?? string.Empty;
                    var race1 = reader["Race1"].ToString() ?? string.Empty;
                    var race2 = reader["Race2"].ToString() ?? string.Empty;
                    var gameDate = DateTime.Parse(reader["GameDate"].ToString() ?? DateTime.MinValue.ToString("O"));
                    var map = reader["Map"].ToString() ?? string.Empty;
                    var replayPath = reader["ReplayFilePath"].ToString() ?? string.Empty;

                    // Determine which player is "you" and which is opponent
                    string yourRace, opponentRaceInMatch, opponentInMatch;

                    if (player1.Contains(yourPlayerName))
                    {
                        yourRace = race1;
                        opponentRaceInMatch = race2;
                        opponentInMatch = player2;
                    }
                    else
                    {
                        yourRace = race2;
                        opponentRaceInMatch = race1;
                        opponentInMatch = player1;
                    }

                    var replayFileName = Path.GetFileName(replayPath);

                    history.Add((opponentInMatch, gameDate, map, yourRace, opponentRaceInMatch, replayFileName));
                }
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
        /// </summary>
        public List<(string, string, string, string, DateTime, string)>
            GetGamesByOpponentId(string yourPlayerId, string opponentPlayerId, int limit = 100)
        {
            var games = new List<(string, string, string, string, DateTime, string)>();

            // Normalize player IDs to handle both formats (with and without region prefix)
            string normalizedYourId = BuildOrderReader.NormalizeToonHandle(yourPlayerId);
            string normalizedOpponentId = BuildOrderReader.NormalizeToonHandle(opponentPlayerId);

            // Extract the last bit for LIKE fallback queries
            string? yourIdLastBit = BuildOrderReader.ExtractToonHandleLastBit(yourPlayerId);
            string? opponentIdLastBit = BuildOrderReader.ExtractToonHandleLastBit(opponentPlayerId);

            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] yourPlayerId={yourPlayerId}, normalized={normalizedYourId}, lastBit={yourIdLastBit}");
            System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] opponentPlayerId={opponentPlayerId}, normalized={normalizedOpponentId}, lastBit={opponentIdLastBit}");

            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Player1, Player2, Race1, Race2, GameDate, Map, Player1Id, Player2Id
                    FROM Replays
                    WHERE (Player1Id = @YourId AND Player2Id = @OpponentId)
                       OR (Player2Id = @YourId AND Player1Id = @OpponentId)
                       OR (Player1Id LIKE ('%' || @YourIdLastBit || '%') AND Player2Id LIKE ('%' || @OpponentIdLastBit || '%'))
                       OR (Player2Id LIKE ('%' || @YourIdLastBit || '%') AND Player1Id LIKE ('%' || @OpponentIdLastBit || '%'))
                    ORDER BY GameDate DESC
                    LIMIT @Limit
                ";
                command.Parameters.AddWithValue("@YourId", normalizedYourId);
                command.Parameters.AddWithValue("@OpponentId", normalizedOpponentId);
                command.Parameters.AddWithValue("@YourIdLastBit", yourIdLastBit ?? string.Empty);
                command.Parameters.AddWithValue("@OpponentIdLastBit", opponentIdLastBit ?? string.Empty);
                command.Parameters.AddWithValue("@Limit", limit);

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var player1 = reader["Player1"].ToString() ?? string.Empty;
                    var player2 = reader["Player2"].ToString() ?? string.Empty;
                    var race1 = reader["Race1"].ToString() ?? string.Empty;
                    var race2 = reader["Race2"].ToString() ?? string.Empty;
                    var gameDate = DateTime.Parse(reader["GameDate"].ToString() ?? DateTime.MinValue.ToString("O"));
                    var map = reader["Map"].ToString() ?? string.Empty;
                    var player1Id = reader["Player1Id"].ToString() ?? string.Empty;

                    // Determine which player is "you"
                    string yourName, opponentName, yourRace, opponentRaceInMatch;

                    string normalizedPlayer1Id = BuildOrderReader.NormalizeToonHandle(player1Id);
                    if (normalizedPlayer1Id == normalizedYourId)
                    {
                        yourName = player1;
                        opponentName = player2;
                        yourRace = race1;
                        opponentRaceInMatch = race2;
                    }
                    else
                    {
                        yourName = player2;
                        opponentName = player1;
                        yourRace = race2;
                        opponentRaceInMatch = race1;
                    }

                    games.Add((yourName, opponentName, yourRace, opponentRaceInMatch, gameDate, map));
                }

                System.Diagnostics.Debug.WriteLine($"[ReplayDatabase.GetGamesByOpponentId] Found {games.Count} games");
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving games by opponent ID: {ex.Message}");
            }

            return games;
        }

        /// <summary>
        /// Get the most recent cached build order for a specific opponent.
        /// </summary>
        public List<(double timeSeconds, string kind, string name)>? GetOpponentLastBuildOrder(string opponentPlayerId, int limit = 20)
        {
            try
            {
                // Normalize player ID to handle both formats
                string normalizedOpponentId = BuildOrderReader.NormalizeToonHandle(opponentPlayerId);
                string? opponentIdLastBit = BuildOrderReader.ExtractToonHandleLastBit(opponentPlayerId);

                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT boe.TimeSeconds, boe.Kind, boe.Name
                    FROM BuildOrderEntries boe
                    INNER JOIN Replays r ON boe.ReplayId = r.Id
                    WHERE (r.Player1Id = @OpponentId OR r.Player2Id = @OpponentId
                           OR r.Player1Id LIKE '%' || @OpponentIdLastBit OR r.Player2Id LIKE '%' || @OpponentIdLastBit)
                      AND r.BuildOrderCached = 1
                    ORDER BY r.GameDate DESC
                    LIMIT @Limit
                ";
                command.Parameters.AddWithValue("@OpponentId", normalizedOpponentId);
                command.Parameters.AddWithValue("@OpponentIdLastBit", opponentIdLastBit ?? string.Empty);
                command.Parameters.AddWithValue("@Limit", limit);

                var buildOrders = new List<(double, string, string)>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    buildOrders.Add((
                        Convert.ToDouble(reader["TimeSeconds"]),
                        reader["Kind"].ToString() ?? string.Empty,
                        reader["Name"].ToString() ?? string.Empty
                    ));
                }

                return buildOrders.Count > 0 ? buildOrders : null;
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error retrieving opponent's last build order: {ex.Message}");
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT ConfigHash, ReplayFolderPath, RecursiveScan, LastValidated
                    FROM ConfigMetadata
                    WHERE Id = 1
                ";

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return (
                        reader["ConfigHash"].ToString() ?? string.Empty,
                        reader["ReplayFolderPath"].ToString() ?? string.Empty,
                        Convert.ToInt32(reader["RecursiveScan"]) == 1,
                        reader["LastValidated"].ToString() ?? DateTime.UtcNow.ToString("O")
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO ConfigMetadata (Id, ConfigHash, ReplayFolderPath, RecursiveScan, LastValidated)
                    VALUES (1, @ConfigHash, @FolderPath, @Recursive, @LastValidated)
                ";
                command.Parameters.AddWithValue("@ConfigHash", configHash);
                command.Parameters.AddWithValue("@FolderPath", folderPath);
                command.Parameters.AddWithValue("@Recursive", recursive ? 1 : 0);
                command.Parameters.AddWithValue("@LastValidated", DateTime.UtcNow.ToString("O"));

                command.ExecuteNonQuery();
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT Id, ReplayFilePath
                    FROM Replays
                    ORDER BY CreatedAt DESC
                ";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    replays.Add(new ReplayRecord
                    {
                        Id = Convert.ToInt64(reader["Id"]),
                        ReplayFilePath = reader["ReplayFilePath"].ToString() ?? string.Empty
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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                // Get all cached file paths
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT ReplayFilePath FROM Replays";

                var cachedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var path = reader["ReplayFilePath"].ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        cachedPaths.Add(path);
                    }
                }

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
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM Replays";
                var total = (long?)command.ExecuteScalar() ?? 0;

                using var command2 = connection.CreateCommand();
                command2.CommandText = "SELECT COUNT(*) FROM Replays WHERE BuildOrderCached = 1";
                var withBuildOrder = (long?)command2.ExecuteScalar() ?? 0;

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
        public void CacheMetadata(ReplayMetadata metadata)
        {
            try
            {
                // If we already have this replay by file path, skip it (idempotent)
                var existingReplay = GetReplayByFilePath(metadata.FilePath);
                if (existingReplay != null)
                {
                    return;
                }

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
