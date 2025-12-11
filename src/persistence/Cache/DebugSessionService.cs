using Serilog;
using SqlKata;
using SqlKata.Compilers;
using System.Data.SQLite;
using System.Security.Cryptography;
using BarcodeRevealTool.Persistence.Schema;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for managing debug session data.
    /// Stores manual opponent entry data, lobby file information, and optionally binary lobby file content.
    /// Only used when running in DEBUG mode.
    /// </summary>
    public class DebugSessionService
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly SqliteCompiler _compiler = new();

        public DebugSessionService(string connectionString, ILogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
            InitializeSchema();
        }

        private void InitializeSchema()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();
                SchemaLoader.ExecuteSchema(connection, "Debug.sql");
                _logger.Information("Debug schema initialized");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize Debug schema");
                throw;
            }
        }

        /// <summary>
        /// Record a debug session with manual opponent entry.
        /// </summary>
        public void RecordManualOpponentSession(int runNumber, string battleTag, string nickname, string? notes = null)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var insertQuery = new Query("DebugSession").AsInsert(new Dictionary<string, object>
                {
                    ["RunNumber"] = runNumber,
                    ["ManualOpponentBattleTag"] = battleTag,
                    ["ManualOpponentNickname"] = nickname,
                    ["DebugMode"] = "ManualEntry",
                    ["StoreLobbyFiles"] = false,
                    ["IsCorrect"] = 1,  // Assume correct until validated
                    ["Notes"] = notes ?? string.Empty
                });
                var compiled = _compiler.Compile(insertQuery);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }
                command.ExecuteNonQuery();

                _logger.Information("Recorded debug session (manual): RunNumber={RunNumber}, BattleTag={BattleTag}, Nickname={Nickname}",
                    runNumber, battleTag, nickname);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record manual opponent debug session");
                throw;
            }
        }

        /// <summary>
        /// Record a debug session with lobby file (optionally storing binary content).
        /// </summary>
        public void RecordLobbyFileSession(int runNumber, string filePath, bool storeLobbyFile, string? notes = null)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                byte[]? lobbyBinary = null;
                long fileSize = 0;

                if (storeLobbyFile && File.Exists(filePath))
                {
                    lobbyBinary = File.ReadAllBytes(filePath);
                    fileSize = lobbyBinary.Length;
                }

                var insertQuery = new Query("DebugSession").AsInsert(new Dictionary<string, object>
                {
                    ["RunNumber"] = runNumber,
                    ["LobbyFilePath"] = filePath,
                    ["LobbyFileName"] = Path.GetFileName(filePath),
                    ["LobbyFileHash"] = ComputeFileHash(filePath),
                    ["LobbyFileBinary"] = (object?)lobbyBinary ?? DBNull.Value,
                    ["LobbyFileSize"] = fileSize,
                    ["DebugMode"] = "LobbyFiles",
                    ["StoreLobbyFiles"] = storeLobbyFile,
                    ["IsCorrect"] = 1,  // Assume correct until validated
                    ["Notes"] = notes ?? string.Empty
                });
                var compiled = _compiler.Compile(insertQuery);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }
                command.ExecuteNonQuery();

                _logger.Information("Recorded debug session (lobby file): RunNumber={RunNumber}, FilePath={FilePath}, StoredBinary={StoredBinary}, FileSize={FileSize}",
                    runNumber, filePath, storeLobbyFile, fileSize);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record lobby file debug session");
                throw;
            }
        }

        /// <summary>
        /// Retrieve debug session information by run number.
        /// </summary>
        public DebugSessionInfo? GetDebugSession(int runNumber)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var selectQuery = new Query("DebugSession")
                    .Where("RunNumber", runNumber)
                    .Select(
                        "RunNumber", "ManualOpponentBattleTag", "ManualOpponentNickname",
                        "LobbyFilePath", "LobbyFileName", "LobbyFileHash", "LobbyFileSize",
                        "DebugMode", "StoreLobbyFiles", "IsCorrect", "DateCreated", "Notes"
                    );
                var compiled = _compiler.Compile(selectQuery);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    return new DebugSessionInfo
                    {
                        RunNumber = reader.GetInt32(0),
                        ManualOpponentBattleTag = reader.IsDBNull(1) ? null : reader.GetString(1),
                        ManualOpponentNickname = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LobbyFilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                        LobbyFileName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        LobbyFileHash = reader.IsDBNull(5) ? null : reader.GetString(5),
                        LobbyFileSize = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                        DebugMode = reader.GetString(7),
                        StoreLobbyFiles = reader.GetBoolean(8),
                        IsCorrect = reader.GetBoolean(9),
                        DateCreated = reader.GetDateTime(10),
                        Notes = reader.IsDBNull(11) ? null : reader.GetString(11)
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve debug session for RunNumber={RunNumber}", runNumber);
            }

            return null;
        }

        /// <summary>
        /// Retrieve the binary lobby file data for a specific run.
        /// Returns null if the file was not stored or doesn't exist.
        /// </summary>
        public byte[]? GetStoredLobbyFileBinary(int runNumber)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var selectQuery = new Query("DebugSession")
                    .Where("RunNumber", runNumber)
                    .Select("LobbyFileBinary");
                var compiled = _compiler.Compile(selectQuery);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                if (reader.Read() && !reader.IsDBNull(0))
                {
                    return (byte[])reader.GetValue(0);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve lobby file binary for RunNumber={RunNumber}", runNumber);
            }

            return null;
        }

        /// <summary>
        /// Get all debug sessions, optionally filtered by date range.
        /// </summary>
        public List<DebugSessionInfo> GetAllDebugSessions(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var sessions = new List<DebugSessionInfo>();

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var selectQuery = new Query("DebugSession")
                    .Select(
                        "RunNumber", "ManualOpponentBattleTag", "ManualOpponentNickname",
                        "LobbyFilePath", "LobbyFileName", "LobbyFileHash", "LobbyFileSize",
                        "DebugMode", "StoreLobbyFiles", "DateCreated", "Notes"
                    );

                if (fromDate.HasValue)
                    selectQuery = selectQuery.Where("DateCreated", ">=", fromDate.Value);
                if (toDate.HasValue)
                    selectQuery = selectQuery.Where("DateCreated", "<=", toDate.Value);

                selectQuery = selectQuery.OrderByDesc("DateCreated");

                var compiled = _compiler.Compile(selectQuery);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sessions.Add(new DebugSessionInfo
                    {
                        RunNumber = reader.GetInt32(0),
                        ManualOpponentBattleTag = reader.IsDBNull(1) ? null : reader.GetString(1),
                        ManualOpponentNickname = reader.IsDBNull(2) ? null : reader.GetString(2),
                        LobbyFilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
                        LobbyFileName = reader.IsDBNull(4) ? null : reader.GetString(4),
                        LobbyFileHash = reader.IsDBNull(5) ? null : reader.GetString(5),
                        LobbyFileSize = reader.IsDBNull(6) ? 0 : reader.GetInt64(6),
                        DebugMode = reader.GetString(7),
                        StoreLobbyFiles = reader.GetBoolean(8),
                        DateCreated = reader.GetDateTime(9),
                        Notes = reader.IsDBNull(10) ? null : reader.GetString(10)
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve all debug sessions");
            }

            return sessions;
        }

        private static string ComputeFileHash(string filePath)
        {
            if (!File.Exists(filePath))
                return string.Empty;

            try
            {
                using var stream = File.OpenRead(filePath);
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Validate a debug session opponent against the user's own battleTag.
        /// If the opponent battleTag matches the user's battleTag, mark the session as incorrect (IsCorrect = 0).
        /// This indicates a bad detection where the system detected the user as the opponent.
        /// </summary>
        public bool ValidateOpponentIsNotUser(int runNumber, string userBattleTag)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                // Get the debug session
                var session = GetDebugSession(runNumber);
                if (session == null)
                {
                    _logger.Warning("Debug session not found for validation: RunNumber={RunNumber}", runNumber);
                    return false;
                }

                // Determine the opponent battleTag to check
                string? opponentBattleTag = session.ManualOpponentBattleTag;
                if (string.IsNullOrEmpty(opponentBattleTag))
                {
                    _logger.Debug("No opponent battleTag to validate for RunNumber={RunNumber}", runNumber);
                    return true; // Not applicable for lobby file mode without opponent tag
                }

                // Normalize both for comparison
                var userTag = userBattleTag?.Trim();
                var opponentTag = opponentBattleTag.Trim();

                // Determine if incorrect: opponent detected as same as user
                bool isIncorrect = string.Equals(userTag, opponentTag, StringComparison.OrdinalIgnoreCase);

                if (isIncorrect)
                {
                    _logger.Warning("Debug session marked as INCORRECT: opponent battleTag matches user's own battleTag. RunNumber={RunNumber}, BattleTag={BattleTag}",
                        runNumber, opponentTag);
                }

                // Update IsCorrect flag
                var updateQuery = new Query("DebugSession")
                    .Where("RunNumber", runNumber)
                    .AsUpdate(new Dictionary<string, object>
                    {
                        ["IsCorrect"] = isIncorrect ? 0 : 1
                    });

                var compiled = _compiler.Compile(updateQuery);
                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                command.Parameters.Clear();
                for (int i = 0; i < compiled.Bindings.Count; i++)
                {
                    command.Parameters.Add(new SQLiteParameter($"@p{i}", compiled.Bindings[i] ?? DBNull.Value));
                }
                command.ExecuteNonQuery();

                return !isIncorrect; // Return true if correct (not a bad detection)
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to validate debug session opponent: RunNumber={RunNumber}", runNumber);
                return false;
            }
        }

        /// <summary>
        /// Get all incorrect debug sessions where opponent was detected as user.
        /// </summary>
        public List<DebugSessionInfo> GetIncorrectSessions(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var sessions = new List<DebugSessionInfo>();

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var selectQuery = new Query("DebugSession")
                    .Where("IsCorrect", 0)
                    .OrderByDesc("DateCreated");

                if (fromDate.HasValue)
                    selectQuery = selectQuery.Where("DateCreated", ">=", fromDate.Value);
                if (toDate.HasValue)
                    selectQuery = selectQuery.Where("DateCreated", "<=", toDate.Value);

                var compiled = _compiler.Compile(selectQuery);
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
                    sessions.Add(new DebugSessionInfo
                    {
                        RunNumber = reader.GetInt32(reader.GetOrdinal("RunNumber")),
                        ManualOpponentBattleTag = reader.IsDBNull(reader.GetOrdinal("ManualOpponentBattleTag")) ? null : reader.GetString(reader.GetOrdinal("ManualOpponentBattleTag")),
                        ManualOpponentNickname = reader.IsDBNull(reader.GetOrdinal("ManualOpponentNickname")) ? null : reader.GetString(reader.GetOrdinal("ManualOpponentNickname")),
                        DebugMode = reader.GetString(reader.GetOrdinal("DebugMode")),
                        StoreLobbyFiles = reader.GetBoolean(reader.GetOrdinal("StoreLobbyFiles")),
                        IsCorrect = reader.GetBoolean(reader.GetOrdinal("IsCorrect")),
                        DateCreated = reader.GetDateTime(reader.GetOrdinal("DateCreated")),
                        Notes = reader.IsDBNull(reader.GetOrdinal("Notes")) ? null : reader.GetString(reader.GetOrdinal("Notes"))
                    });
                }

                _logger.Debug("Retrieved {IncorrectSessionCount} incorrect debug sessions", sessions.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get incorrect debug sessions");
            }

            return sessions;
        }
    }

    /// <summary>
    /// Data model for debug session information.
    /// </summary>
    public class DebugSessionInfo
    {
        public int RunNumber { get; set; }
        public string? ManualOpponentBattleTag { get; set; }
        public string? ManualOpponentNickname { get; set; }
        public string? LobbyFilePath { get; set; }
        public string? LobbyFileName { get; set; }
        public string? LobbyFileHash { get; set; }
        public long LobbyFileSize { get; set; }
        public string DebugMode { get; set; } = string.Empty;  // "ManualEntry" or "LobbyFiles"
        public bool StoreLobbyFiles { get; set; }
        public bool IsCorrect { get; set; } = true;  // false if opponent battleTag matched user's own (bad detection)
        public DateTime DateCreated { get; set; }
        public string? Notes { get; set; }
    }
}
