using System.Data.SQLite;

namespace BarcodeRevealTool.Replay
{
    /// <summary>
    /// SQLite-based cache for replay metadata to avoid re-processing replays.
    /// </summary>
    public class ReplayCacheDatabase
    {
        private readonly string _databasePath;
        private const string DatabaseFileName = "replay_cache.db";

        public ReplayCacheDatabase(string? customPath = null)
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
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS ReplayCache (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FilePath TEXT NOT NULL UNIQUE,
                    FileHash TEXT NOT NULL,
                    LastModified TEXT NOT NULL,
                    CachedAt TEXT NOT NULL,
                    PlayersJson TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_FilePath ON ReplayCache(FilePath);
                CREATE INDEX IF NOT EXISTS idx_LastModified ON ReplayCache(LastModified);
            ";
            command.ExecuteNonQuery();
        }

        /// <summary>
        /// Get cached metadata for a replay file, or null if not in cache.
        /// </summary>
        public ReplayMetadata? GetCachedMetadata(string filePath)
        {
            try
            {
                var fileHash = ComputeFileHash(filePath);
                var fileInfo = new FileInfo(filePath);

                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT PlayersJson, LastModified
                    FROM ReplayCache
                    WHERE FilePath = @FilePath AND FileHash = @FileHash AND LastModified = @LastModified
                ";
                command.Parameters.AddWithValue("@FilePath", filePath);
                command.Parameters.AddWithValue("@FileHash", fileHash);
                command.Parameters.AddWithValue("@LastModified", fileInfo.LastWriteTime.ToString("O"));

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var playersJson = reader["PlayersJson"].ToString();
                    var players = System.Text.Json.JsonSerializer.Deserialize<List<PlayerInfo>>(playersJson ?? "[]") ?? new();

                    return new ReplayMetadata
                    {
                        FilePath = filePath,
                        Players = players,
                        LastModified = fileInfo.LastWriteTime
                    };
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving cached metadata: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Cache replay metadata to the database.
        /// </summary>
        public void CacheMetadata(ReplayMetadata metadata)
        {
            try
            {
                var fileHash = ComputeFileHash(metadata.FilePath);
                var playersJson = System.Text.Json.JsonSerializer.Serialize(metadata.Players);

                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT OR REPLACE INTO ReplayCache
                    (FilePath, FileHash, LastModified, CachedAt, PlayersJson)
                    VALUES (@FilePath, @FileHash, @LastModified, @CachedAt, @PlayersJson)
                ";
                command.Parameters.AddWithValue("@FilePath", metadata.FilePath);
                command.Parameters.AddWithValue("@FileHash", fileHash);
                command.Parameters.AddWithValue("@LastModified", metadata.LastModified.ToString("O"));
                command.Parameters.AddWithValue("@CachedAt", DateTime.UtcNow.ToString("O"));
                command.Parameters.AddWithValue("@PlayersJson", playersJson);

                command.ExecuteNonQuery();
                Console.WriteLine($"  💾 Cached: {Path.GetFileName(metadata.FilePath)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching metadata: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all cached replays with a specific player.
        /// </summary>
        public List<ReplayMetadata> GetCachedReplaysWithPlayer(string playerIdentifier)
        {
            var results = new List<ReplayMetadata>();

            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT FilePath, PlayersJson, LastModified FROM ReplayCache";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var filePath = reader["FilePath"].ToString();
                    var playersJson = reader["PlayersJson"].ToString();
                    var lastModified = DateTime.Parse(reader["LastModified"].ToString() ?? DateTime.MinValue.ToString("O"));

                    var players = System.Text.Json.JsonSerializer.Deserialize<List<PlayerInfo>>(playersJson ?? "[]") ?? new();

                    // Check if any player matches the identifier
                    if (players.Any(p =>
                        p.BattleTag.Equals(playerIdentifier, StringComparison.OrdinalIgnoreCase) ||
                        p.Name.Contains(playerIdentifier, StringComparison.OrdinalIgnoreCase)))
                    {
                        results.Add(new ReplayMetadata
                        {
                            FilePath = filePath ?? string.Empty,
                            Players = players,
                            LastModified = lastModified
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving cached replays: {ex.Message}");
            }

            return results;
        }

        /// <summary>
        /// Clear old cache entries (older than specified days).
        /// </summary>
        public void ClearOldCache(int daysOld = 30)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM ReplayCache WHERE CachedAt < @CutoffDate";
                command.Parameters.AddWithValue("@CutoffDate", cutoffDate.ToString("O"));

                var deletedRows = command.ExecuteNonQuery();
                if (deletedRows > 0)
                {
                    Console.WriteLine($"Cleared {deletedRows} old cache entries.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing old cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Compute a simple hash of the file (size + first/last bytes) for quick validation.
        /// </summary>
        private static string ComputeFileHash(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var hash = $"{fileInfo.Length}";

                using var file = File.OpenRead(filePath);
                byte[] buffer = new byte[1024];

                // Read first chunk
                file.Read(buffer, 0, 1024);
                hash += BitConverter.ToString(buffer, 0, Math.Min(32, buffer.Length));

                // Read last chunk
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

        /// <summary>
        /// Get database statistics.
        /// </summary>
        public (int Total, int WithPlayers) GetCacheStats()
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM ReplayCache";
                var total = (long?)command.ExecuteScalar() ?? 0;

                using var command2 = connection.CreateCommand();
                command2.CommandText = "SELECT COUNT(*) FROM ReplayCache WHERE PlayersJson != '[]'";
                var withPlayers = (long?)command2.ExecuteScalar() ?? 0;

                return ((int)total, (int)withPlayers);
            }
            catch
            {
                return (0, 0);
            }
        }
    }
}