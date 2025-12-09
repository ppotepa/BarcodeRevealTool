using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using BarcodeRevealTool.Engine.Domain.Models;
using SqlKata;
using SqlKata.Compilers;
using Serilog;

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
            using var command = connection.CreateCommand();

            _logger.Information("Initializing replay database schema");

            command.CommandText = @"
CREATE TABLE IF NOT EXISTS Replays (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReplayGuid TEXT UNIQUE NOT NULL,
    YourPlayer TEXT,
    OpponentPlayer TEXT,
    Map TEXT,
    YourRace TEXT,
    OpponentRace TEXT,
    GameDate TEXT NOT NULL,
    ReplayFilePath TEXT UNIQUE NOT NULL,
    FileHash TEXT,
    SC2ClientVersion TEXT,
    YourPlayerId TEXT,
    OpponentPlayerId TEXT,
    BuildOrderCached INTEGER DEFAULT 0,
    CachedAt TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_replays_opponent ON Replays(OpponentPlayer);
CREATE INDEX IF NOT EXISTS idx_replays_gamedate ON Replays(GameDate DESC);
CREATE INDEX IF NOT EXISTS idx_replays_filepath ON Replays(ReplayFilePath);

CREATE TABLE IF NOT EXISTS BuildOrderEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReplayId INTEGER NOT NULL,
    PlayerId TEXT,
    TimeSeconds INTEGER,
    Kind TEXT,
    Name TEXT,
    FOREIGN KEY (ReplayId) REFERENCES Replays(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS idx_buildorder_replayid ON BuildOrderEntries(ReplayId);
";

            try
            {
                command.ExecuteNonQuery();
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
                using var command = connection.CreateCommand();

                var query = new Query("Replays")
                    .Where("ReplayGuid", metadata.ReplayGuid)
                    .Select("Id");

                var compiled = _compiler.Compile(query);
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                var existingId = command.ExecuteScalar();
                if (existingId != null)
                {
                    _logger.Debug("Replay already exists: {ReplayGuid}", metadata.ReplayGuid);
                    return Convert.ToInt64(existingId);
                }

                // Insert new replay
                var insertQuery = new Query("Replays").AsInsert(new
                {
                    ReplayGuid = metadata.ReplayGuid,
                    YourPlayer = metadata.YourPlayer,
                    OpponentPlayer = metadata.OpponentPlayer,
                    Map = metadata.Map,
                    YourRace = metadata.YourRace,
                    OpponentRace = metadata.OpponentRace,
                    GameDate = metadata.GameDate.ToString("O"),
                    ReplayFilePath = metadata.ReplayFilePath,
                    FileHash = ComputeFileHash(metadata.ReplayFilePath),
                    SC2ClientVersion = metadata.SC2ClientVersion,
                    YourPlayerId = metadata.YourPlayerId,
                    OpponentPlayerId = metadata.OpponentPlayerId,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    UpdatedAt = DateTime.UtcNow.ToString("O")
                });

                var insertCompiled = _compiler.Compile(insertQuery);
                command.CommandText = insertCompiled.Sql;
                command.Parameters.Clear();
                foreach (var binding in insertCompiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                command.ExecuteNonQuery();

                // Get the inserted ID
                command.CommandText = "SELECT last_insert_rowid()";
                var newId = command.ExecuteScalar();
                
                _logger.Debug("Stored replay: {YourPlayer} vs {OpponentPlayer} on {Map}", 
                    metadata.YourPlayer, metadata.OpponentPlayer, metadata.Map);
                
                return Convert.ToInt64(newId ?? 0);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to add/update replay");
                return 0;
            }
        }

        public List<string> GetMissingReplayFiles(string[] allReplayFilesOnDisk)
        {
            try
            {
                using var connection = CreateConnection();

                var query = new Query("Replays")
                    .Select("ReplayFilePath");

                var compiled = _compiler.Compile(query);
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

                var missingFiles = allReplayFilesOnDisk
                    .Where(f => !cachedFiles.Contains(f))
                    .ToList();

                _logger.Debug("Found {MissingCount} missing replay files out of {TotalCount}",
                    missingFiles.Count, allReplayFilesOnDisk.Length);

                return missingFiles;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get missing replay files");
                return allReplayFilesOnDisk.ToList();
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
