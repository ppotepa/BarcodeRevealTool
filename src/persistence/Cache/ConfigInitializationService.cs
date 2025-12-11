using Serilog;
using SqlKata;
using SqlKata.Compilers;
using System.Data.SQLite;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for managing configuration initialization on startup.
    /// Populates ConfigHistory with current values when application starts.
    /// Fires events when configuration changes occur.
    /// </summary>
    public class ConfigInitializationService
    {
        private readonly string _connectionString;
        private readonly SqliteCompiler _compiler = new();
        private readonly ILogger _logger = Log.ForContext<ConfigInitializationService>();

        public event EventHandler<ConfigChangeEventArgs>? ConfigChanged;

        public ConfigInitializationService(string? customDatabasePath = null)
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
        }

        private SQLiteConnection CreateConnection()
        {
            var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Initialize config history on startup.
        /// Creates entries for all existing configurations in ConfigHistory table.
        /// This ensures we have a baseline of configuration state at startup.
        /// </summary>
        public void InitializeConfigHistoryOnStartup(int runNumber)
        {
            try
            {
                using var connection = CreateConnection();

                // Get all current configurations
                var query = new Query("UserConfig").Select("ConfigKey", "ConfigValue");
                var compiled = _compiler.Compile(query);

                var currentConfigs = new Dictionary<string, string>();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var key = reader["ConfigKey"].ToString() ?? "";
                            var value = reader["ConfigValue"].ToString() ?? "";
                            currentConfigs[key] = value;
                        }
                    }
                }

                // Insert startup snapshot into ConfigHistory
                foreach (var kvp in currentConfigs)
                {
                    InsertConfigHistoryEntry(
                        kvp.Key,
                        oldValue: null, // No previous value at startup
                        kvp.Value,
                        runNumber,
                        "Startup"
                    );
                }

                _logger.Information("Initialized {Count} configuration entries in ConfigHistory on startup",
                    currentConfigs.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize config history on startup");
            }
        }

        /// <summary>
        /// Record a configuration change and fire the ConfigChanged event.
        /// </summary>
        public void RecordConfigChange(string key, string? oldValue, string newValue, int runNumber, string source = "Manual")
        {
            try
            {
                // Update UserConfig
                using var connection = CreateConnection();
                var updateQuery = new Query("UserConfig")
                    .Where("ConfigKey", key)
                    .AsUpdate(new
                    {
                        ConfigValue = newValue,
                        LastModified = DateTime.UtcNow.ToString("O"),
                        ModifiedByRunNumber = runNumber,
                        IsDefault = 0
                    });

                var compiled = _compiler.Compile(updateQuery);
                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }
                command.ExecuteNonQuery();

                // Insert into ConfigHistory
                InsertConfigHistoryEntry(key, oldValue, newValue, runNumber, source);

                // Fire event
                ConfigChanged?.Invoke(this, new ConfigChangeEventArgs
                {
                    ConfigKey = key,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedAt = DateTime.UtcNow,
                    RunNumber = runNumber,
                    Source = source
                });

                _logger.Information("Configuration changed: {Key} -> {NewValue} (source: {Source})",
                    key, newValue, source);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record configuration change for {Key}", key);
            }
        }

        private void InsertConfigHistoryEntry(string key, string? oldValue, string newValue, int runNumber, string source)
        {
            try
            {
                using var connection = CreateConnection();
                var insertQuery = new Query("ConfigHistory").AsInsert(new
                {
                    ConfigKey = key,
                    OldValue = oldValue,
                    NewValue = newValue,
                    ChangedAt = DateTime.UtcNow.ToString("O"),
                    RunNumber = runNumber,
                    ChangeSource = source
                });

                var compiled = _compiler.Compile(insertQuery);
                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }
                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to insert config history entry for {Key}", key);
            }
        }

        /// <summary>
        /// Get configuration change history for a specific key.
        /// </summary>
        public List<ConfigChangeRecord> GetConfigChangeHistory(string key, int limit = 50)
        {
            try
            {
                using var connection = CreateConnection();
                var query = new Query("ConfigHistory")
                    .Where("ConfigKey", key)
                    .OrderByDesc("ChangedAt")
                    .Limit(limit);

                var compiled = _compiler.Compile(query);
                var result = new List<ConfigChangeRecord>();

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(new ConfigChangeRecord
                        {
                            ConfigKey = reader["ConfigKey"].ToString() ?? "",
                            OldValue = reader["OldValue"]?.ToString(),
                            NewValue = reader["NewValue"].ToString() ?? "",
                            ChangedAt = DateTime.Parse(reader["ChangedAt"].ToString() ?? DateTime.UtcNow.ToString("O")),
                            RunNumber = Convert.ToInt32(reader["RunNumber"] ?? 0),
                            Source = reader["ChangeSource"].ToString() ?? "Unknown"
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get config change history for {Key}", key);
                return new List<ConfigChangeRecord>();
            }
        }
    }

    /// <summary>
    /// Event arguments for configuration changes.
    /// </summary>
    public class ConfigChangeEventArgs : EventArgs
    {
        public string ConfigKey { get; set; } = "";
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = "";
        public DateTime ChangedAt { get; set; }
        public int RunNumber { get; set; }
        public string Source { get; set; } = "Manual";
    }

    /// <summary>
    /// Record of a configuration change.
    /// </summary>
    public class ConfigChangeRecord
    {
        public string ConfigKey { get; set; } = "";
        public string? OldValue { get; set; }
        public string NewValue { get; set; } = "";
        public DateTime ChangedAt { get; set; }
        public int RunNumber { get; set; }
        public string Source { get; set; } = "Unknown";
    }
}
