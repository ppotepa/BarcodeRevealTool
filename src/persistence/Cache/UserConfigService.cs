using Serilog;
using SqlKata;
using SqlKata.Compilers;
using System.Data.SQLite;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for managing user configuration settings and tracking changes.
    /// Stores configuration in the UserConfig table and tracks history in ConfigHistory.
    /// </summary>
    public class UserConfigService
    {
        private readonly string _connectionString;
        private readonly SqliteCompiler _compiler = new();
        private readonly ILogger _logger = Log.ForContext<UserConfigService>();

        public UserConfigService(string? customDatabasePath = null)
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

            _logger.Information("UserConfigService initialized at {DatabasePath}", databasePath);
            InitializeSchema();
        }

        private void InitializeSchema()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                BarcodeRevealTool.Persistence.Schema.SchemaLoader.ExecuteSchema(connection, "UserConfig.sql");

                _logger.Debug("UserConfig schema initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize UserConfig schema");
                throw;
            }
        }

        private SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Get a configuration value by key.
        /// </summary>
        public string? GetConfig(string key)
        {
            try
            {
                using var connection = CreateConnection();
                var query = new Query("UserConfig")
                    .Where("ConfigKey", key)
                    .Select("ConfigValue");

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
                    _logger.Debug("Retrieved config {Key}: {Value}", key, result);
                    return result.ToString();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get config {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Set a configuration value and track the change in history.
        /// Returns true if the value was changed, false if it was already set to the same value.
        /// </summary>
        public bool SetConfig(string key, string value, string valueType = "String", int? runId = null, string? notes = null)
        {
            try
            {
                using var connection = CreateConnection();
                var oldValue = GetConfig(key);
                bool changed = oldValue != value;

                if (!changed)
                {
                    _logger.Debug("Config {Key} unchanged (already set to {Value})", key, value);
                    return false;
                }

                // Update or insert config
                var now = DateTime.UtcNow.ToString("O");
                var upsertQuery = new Query("UserConfig")
                    .Where("ConfigKey", key);

                var checkCompiled = _compiler.Compile(new Query("UserConfig").Where("ConfigKey", key).Select("Id"));
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = checkCompiled.Sql;
                checkCommand.Parameters.Clear();
                for (int i = 0; i < checkCompiled.Bindings.Count; i++)
                {
                    checkCommand.Parameters.Add(new SQLiteParameter($"@p{i}", checkCompiled.Bindings[i] ?? DBNull.Value));
                }

                var exists = checkCommand.ExecuteScalar();

                if (exists != null)
                {
                    // Update existing
                    var updateQuery = new Query("UserConfig")
                        .Where("ConfigKey", key)
                        .AsUpdate(new Dictionary<string, object>
                        {
                            ["ConfigValue"] = value,
                            ["ValueType"] = valueType,
                            ["LastModified"] = now,
                            ["ModifiedByRunId"] = runId ?? (object)DBNull.Value,
                            ["Notes"] = notes ?? (object)DBNull.Value
                        });

                    var updateCompiled = _compiler.Compile(updateQuery);
                    using var updateCommand = connection.CreateCommand();
                    updateCommand.CommandText = updateCompiled.Sql;
                    updateCommand.Parameters.Clear();
                    for (int i = 0; i < updateCompiled.Bindings.Count; i++)
                    {
                        updateCommand.Parameters.Add(new SQLiteParameter($"@p{i}", updateCompiled.Bindings[i] ?? DBNull.Value));
                    }
                    updateCommand.ExecuteNonQuery();
                }
                else
                {
                    // Insert new
                    var insertQuery = new Query("UserConfig").AsInsert(new Dictionary<string, object>
                    {
                        ["ConfigKey"] = key,
                        ["ConfigValue"] = value,
                        ["ValueType"] = valueType,
                        ["LastModified"] = now,
                        ["ModifiedByRunId"] = runId ?? (object)DBNull.Value,
                        ["Notes"] = notes ?? (object)DBNull.Value
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
                }

                // Record in history
                if (oldValue != null) // Only record history if there was a change
                {
                    var historyQuery = new Query("ConfigHistory").AsInsert(new Dictionary<string, object>
                    {
                        ["ConfigKey"] = key,
                        ["OldValue"] = oldValue ?? (object)DBNull.Value,
                        ["NewValue"] = value,
                        ["ChangedAt"] = now,
                        ["RunId"] = runId ?? (object)DBNull.Value
                    });

                    var historyCompiled = _compiler.Compile(historyQuery);
                    using var historyCommand = connection.CreateCommand();
                    historyCommand.CommandText = historyCompiled.Sql;
                    historyCommand.Parameters.Clear();
                    for (int i = 0; i < historyCompiled.Bindings.Count; i++)
                    {
                        historyCommand.Parameters.Add(new SQLiteParameter($"@p{i}", historyCompiled.Bindings[i] ?? DBNull.Value));
                    }
                    historyCommand.ExecuteNonQuery();
                }

                _logger.Information("Config {Key} changed from {OldValue} to {NewValue}", key, oldValue ?? "(null)", value);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to set config {Key} to {Value}", key, value);
                return false;
            }
        }

        /// <summary>
        /// Get all configuration values as a dictionary.
        /// </summary>
        public Dictionary<string, string> GetAllConfigs()
        {
            try
            {
                using var connection = CreateConnection();
                var query = new Query("UserConfig")
                    .Select("ConfigKey", "ConfigValue");

                var compiled = _compiler.Compile(query);
                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                command.Parameters.Clear();

                var result = new Dictionary<string, string>();
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var key = reader["ConfigKey"].ToString();
                    var value = reader["ConfigValue"].ToString();
                    if (key != null && value != null)
                    {
                        result[key] = value;
                    }
                }

                _logger.Debug("Retrieved {ConfigCount} configuration values", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get all configs");
                return new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// Delete all configuration settings (useful for reset).
        /// </summary>
        public bool ClearAllConfigs()
        {
            try
            {
                using var connection = CreateConnection();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM UserConfig;";
                command.ExecuteNonQuery();

                _logger.Warning("All configuration settings cleared");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to clear all configs");
                return false;
            }
        }
    }
}
