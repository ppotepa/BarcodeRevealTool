using Serilog;
using System.Data.SQLite;
using System.Reflection;
using Spectre.Console;

namespace BarcodeRevealTool.Persistence.Schema.Migrations
{
    /// <summary>
    /// Service for managing and executing database migrations.
    /// Migrations are versioned SQL files that are executed in order to evolve the schema.
    /// </summary>
    public class MigrationRunner
    {
        private readonly string _connectionString;
        private readonly ILogger _logger = Log.ForContext<MigrationRunner>();

        public MigrationRunner(string connectionString)
        {
            _connectionString = connectionString;
            InitializeMigrationTracking();
        }

        /// <summary>
        /// Initialize the migration tracking table if it doesn't exist.
        /// </summary>
        private void InitializeMigrationTracking()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    CREATE TABLE IF NOT EXISTS __MigrationHistory (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        MigrationName TEXT NOT NULL UNIQUE,
                        Version TEXT NOT NULL,
                        ExecutedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        ExecutionTimeMs INTEGER,
                        Status TEXT NOT NULL DEFAULT 'Success',
                        ErrorMessage TEXT
                    );
                    
                    CREATE INDEX IF NOT EXISTS idx_migration_name ON __MigrationHistory(MigrationName);
                    CREATE INDEX IF NOT EXISTS idx_migration_version ON __MigrationHistory(Version);
                ";

                command.ExecuteNonQuery();
                _logger.Debug("Migration tracking table initialized");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize migration tracking table");
                throw;
            }
        }

        /// <summary>
        /// Run all pending migrations in order.
        /// </summary>
        public async Task<MigrationResult> RunAllMigrationsAsync()
        {
            var result = new MigrationResult();

            try
            {
                var migrations = GetAllMigrations();

                if (!migrations.Any())
                {
                    _logger.Warning("No migrations found. This may indicate an issue with embedded resources. Continuing anyway.");
                    result.Success = true;
                    result.TotalMigrations = 0;
                    return result;
                }

                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                result.TotalMigrations = migrations.Count;
                _logger.Information("Starting migration execution. Found {MigrationCount} migrations", migrations.Count);

                // Display progress bar using Spectre.Console
                await AnsiConsole.Progress()
                    .StartAsync(async ctx =>
                    {
                        var task = ctx.AddTask("[bold cyan]Running Migrations[/]", maxValue: migrations.Count);

                        foreach (var migration in migrations)
                        {
                            var executed = IsMigrationExecuted(connection, migration.Name);
                            if (executed)
                            {
                                _logger.Debug("Migration already executed: {MigrationName}", migration.Name);
                                result.SkippedMigrations++;
                                task.Increment(1);
                                continue;
                            }

                            task.Description = $"[bold cyan]{migration.Name}[/]";
                            _logger.Information("Executing migration: {MigrationName} (v{Version})", migration.Name, migration.Version);
                            await ExecuteMigrationAsync(connection, migration, result);
                            task.Increment(1);
                        }
                    });

                result.Success = true;
                _logger.Information("All migrations completed successfully. Executed: {Executed}, Skipped: {Skipped}",
                    result.ExecutedMigrations, result.SkippedMigrations);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.Error(ex, "Migration execution failed");
            }

            return result;
        }

        /// <summary>
        /// Execute a single migration.
        /// </summary>
        private async Task ExecuteMigrationAsync(SQLiteConnection connection, MigrationInfo migration, MigrationResult result)
        {
            var startTime = DateTime.UtcNow;

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = migration.Sql;

                await Task.Run(() => command.ExecuteNonQuery());

                var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                RecordMigration(connection, migration.Name, migration.Version, (long)executionTime, "Success", null);

                _logger.Information("Migration executed successfully: {MigrationName} (v{Version}) in {ExecutionTime}ms",
                    migration.Name, migration.Version, executionTime);

                result.ExecutedMigrations++;
            }
            catch (Exception ex)
            {
                var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                RecordMigration(connection, migration.Name, migration.Version, (long)executionTime, "Failed", ex.Message);

                _logger.Error(ex, "Migration failed: {MigrationName} (v{Version})", migration.Name, migration.Version);

                result.FailedMigrations++;
                result.ErrorMessage = ex.Message;

                throw;
            }
        }

        /// <summary>
        /// Get all pending migrations in order.
        /// </summary>
        private List<MigrationInfo> GetAllMigrations()
        {
            var migrations = new List<MigrationInfo>();

            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var allResourceNames = assembly.GetManifestResourceNames();

                _logger.Debug("Available resources ({Count}): {Resources}",
                    allResourceNames.Length,
                    string.Join(", ", allResourceNames.Where(n => n.Contains("Schema")).Take(10)));

                var resourceNames = allResourceNames
                    .Where(name => name.Contains("Schema.Migrations.V") && name.EndsWith(".sql"))
                    .OrderBy(name => name)
                    .ToList();

                _logger.Information("Found {MigrationCount} migration SQL files", resourceNames.Count);
                if (resourceNames.Count > 0)
                {
                    _logger.Debug("Migration files: {Files}", string.Join(", ", resourceNames));
                }

                foreach (var resourceName in resourceNames)
                {
                    try
                    {
                        using var stream = assembly.GetManifestResourceStream(resourceName);
                        if (stream == null)
                        {
                            _logger.Warning("Stream is null for resource: {ResourceName}", resourceName);
                            continue;
                        }

                        using var reader = new StreamReader(stream);
                        var sql = reader.ReadToEnd();

                        var parts = resourceName.Split('.');
                        var versionAndName = string.Join(".", parts.Skip(Math.Max(0, parts.Length - 2)).Take(2));
                        var version = parts[parts.Length - 3];

                        migrations.Add(new MigrationInfo
                        {
                            Name = versionAndName,
                            Version = version,
                            Sql = sql,
                            ResourceName = resourceName
                        });

                        _logger.Debug("Loaded migration: {Name} (v{Version})", versionAndName, version);
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Failed to load migration: {ResourceName}", resourceName);
                    }
                }

                _logger.Information("Successfully loaded {MigrationCount} migrations", migrations.Count);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get migrations from resources");
            }

            return migrations;
        }

        /// <summary>
        /// Check if a migration has already been executed.
        /// </summary>
        private bool IsMigrationExecuted(SQLiteConnection connection, string migrationName)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT COUNT(*) FROM __MigrationHistory WHERE MigrationName = @name AND Status = 'Success';";
                command.Parameters.AddWithValue("@name", migrationName);

                var result = command.ExecuteScalar();
                var count = result != null ? Convert.ToInt64(result) : 0;
                return count > 0;
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to check migration status for {MigrationName}", migrationName);
                return false;
            }
        }

        /// <summary>
        /// Record a migration execution in the history table.
        /// </summary>
        private void RecordMigration(SQLiteConnection connection, string migrationName, string version,
            long executionTimeMs, string status, string? errorMessage)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
                    INSERT INTO __MigrationHistory (MigrationName, Version, ExecutionTimeMs, Status, ErrorMessage)
                    VALUES (@name, @version, @executionTime, @status, @errorMessage);
                ";

                command.Parameters.AddWithValue("@name", migrationName);
                command.Parameters.AddWithValue("@version", version);
                command.Parameters.AddWithValue("@executionTime", executionTimeMs);
                command.Parameters.AddWithValue("@status", status);
                command.Parameters.AddWithValue("@errorMessage", errorMessage ?? (object)DBNull.Value);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to record migration in history: {MigrationName}", migrationName);
            }
        }

        /// <summary>
        /// Get the migration history.
        /// </summary>
        public List<MigrationHistoryEntry> GetMigrationHistory()
        {
            var history = new List<MigrationHistoryEntry>();

            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT MigrationName, Version, ExecutedAt, ExecutionTimeMs, Status, ErrorMessage
                    FROM __MigrationHistory
                    ORDER BY ExecutedAt DESC;
                ";

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    history.Add(new MigrationHistoryEntry
                    {
                        MigrationName = reader["MigrationName"].ToString() ?? string.Empty,
                        Version = reader["Version"].ToString() ?? string.Empty,
                        ExecutedAt = DateTime.Parse(reader["ExecutedAt"].ToString() ?? DateTime.UtcNow.ToString("O")),
                        ExecutionTimeMs = Convert.ToInt64(reader["ExecutionTimeMs"] ?? 0),
                        Status = reader["Status"].ToString() ?? "Unknown",
                        ErrorMessage = reader["ErrorMessage"] != DBNull.Value ? reader["ErrorMessage"].ToString() : null
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve migration history");
            }

            return history;
        }
    }

    /// <summary>
    /// Result of running migrations.
    /// </summary>
    public class MigrationResult
    {
        public bool Success { get; set; }
        public int TotalMigrations { get; set; }
        public int ExecutedMigrations { get; set; }
        public int SkippedMigrations { get; set; }
        public int FailedMigrations { get; set; }
        public string? ErrorMessage { get; set; }

        public override string ToString()
        {
            return $"Migrations: Executed={ExecutedMigrations}, Skipped={SkippedMigrations}, Failed={FailedMigrations}, " +
                   $"Status={(Success ? "Success" : "Failed")}";
        }
    }

    /// <summary>
    /// Entry in the migration history.
    /// </summary>
    public class MigrationHistoryEntry
    {
        public string MigrationName { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public long ExecutionTimeMs { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Information about a single migration.
    /// </summary>
    public class MigrationInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Sql { get; set; } = string.Empty;
        public string ResourceName { get; set; } = string.Empty;
    }
}
