using Serilog;
using SqlKata;
using SqlKata.Compilers;
using System.Data.SQLite;
using BarcodeRevealTool.Persistence.Schema;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for managing run information and numbering in the database.
    /// Provides sequential run numbers for logging and tracking purposes.
    /// </summary>
    public class RunInfoService
    {
        private readonly string _connectionString;
        private readonly ILogger _logger;
        private readonly SqliteCompiler _compiler = new();

        public RunInfoService(string connectionString, ILogger logger)
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
                SchemaLoader.ExecuteSchema(connection, "RunInfo.sql");
                _logger.Information("RunInfo schema initialized");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize RunInfo schema");
                throw;
            }
        }

        /// <summary>
        /// Gets the next run number with daily reset logic.
        /// Keeps max 2 records per mode (one per day), resets counter if date changes.
        /// Creates a new RunInfo entry and returns the assigned run number.
        /// </summary>
        public int GetNextRunNumber(string mode = "Debug")
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var today = DateTime.UtcNow.Date;

                // Get today's records for this mode
                var getTodayQuery = new Query("RunInfo")
                    .Where("Mode", mode)
                    .WhereRaw($"DATE(DateStarted) = DATE('{today:yyyy-MM-dd}')");
                var getTodayCompiled = _compiler.Compile(getTodayQuery);

                int nextRunNumber = 1;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = getTodayCompiled.Sql;
                    foreach (var binding in getTodayCompiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }

                    using var reader = command.ExecuteReader();
                    int maxTodayRun = 0;
                    while (reader.Read())
                    {
                        int runNum = reader.GetInt32(0);
                        if (runNum > maxTodayRun)
                            maxTodayRun = runNum;
                    }

                    nextRunNumber = maxTodayRun + 1;
                }

                // Clean up old records - keep max 2 per mode (today's records only)
                var deleteOldQuery = new Query("RunInfo")
                    .Where("Mode", mode)
                    .WhereRaw($"DATE(DateStarted) < DATE('{today:yyyy-MM-dd}')");
                var deleteOldCompiled = _compiler.Compile(deleteOldQuery.AsDelete());

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = deleteOldCompiled.Sql;
                    foreach (var binding in deleteOldCompiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    command.ExecuteNonQuery();
                }

                // Insert new run entry
                var insertQuery = new Query("RunInfo").AsInsert(new Dictionary<string, object>
                {
                    ["RunNumber"] = nextRunNumber,
                    ["DateStarted"] = DateTime.UtcNow,
                    ["DateResetAt"] = today,
                    ["Status"] = "InProgress",
                    ["Mode"] = mode
                });
                var insertCompiled = _compiler.Compile(insertQuery);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = insertCompiled.Sql;
                    foreach (var binding in insertCompiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    command.ExecuteNonQuery();
                }

                _logger.Information("Created new run entry with RunNumber: {RunNumber} for mode: {Mode}", nextRunNumber, mode);
                return nextRunNumber;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get next run number");
                throw;
            }
        }

        /// <summary>
        /// Marks the current run as completed with final statistics.
        /// </summary>
        public void CompleteRun(int runNumber, int totalReplaysProcessed)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var updateQuery = new Query("RunInfo")
                    .Where("RunNumber", runNumber)
                    .AsUpdate(new Dictionary<string, object>
                    {
                        ["DateCompleted"] = DateTime.UtcNow,
                        ["Status"] = "Completed",
                        ["TotalReplaysProcessed"] = totalReplaysProcessed
                    });
                var updateCompiled = _compiler.Compile(updateQuery);

                using var command = connection.CreateCommand();
                command.CommandText = updateCompiled.Sql;
                foreach (var binding in updateCompiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }
                command.ExecuteNonQuery();
                _logger.Information("Run {RunNumber} marked as completed with {ReplayCount} replays", runNumber, totalReplaysProcessed);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to complete run {RunNumber}", runNumber);
            }
        }

        /// <summary>
        /// Marks the current run as failed with error notes.
        /// </summary>
        public void FailRun(int runNumber, string errorMessage)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var updateQuery = new Query("RunInfo")
                    .Where("RunNumber", runNumber)
                    .AsUpdate(new Dictionary<string, object>
                    {
                        ["DateCompleted"] = DateTime.UtcNow,
                        ["Status"] = "Failed",
                        ["Notes"] = errorMessage ?? "Unknown error"
                    });
                var updateCompiled = _compiler.Compile(updateQuery);

                using var command = connection.CreateCommand();
                command.CommandText = updateCompiled.Sql;
                foreach (var binding in updateCompiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }
                command.ExecuteNonQuery();
                _logger.Information("Run {RunNumber} marked as failed", runNumber);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to mark run {RunNumber} as failed", runNumber);
            }
        }

        /// <summary>
        /// Gets information about a specific run.
        /// </summary>
        public (int RunNumber, DateTime DateStarted, DateTime? DateCompleted, string Status) GetRunInfo(int runNumber)
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                var selectQuery = new Query("RunInfo")
                    .Where("RunNumber", runNumber)
                    .Select("RunNumber", "DateStarted", "DateCompleted", "Status");
                var selectCompiled = _compiler.Compile(selectQuery);

                using var command = connection.CreateCommand();
                command.CommandText = selectCompiled.Sql;
                foreach (var binding in selectCompiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                if (reader.Read())
                {
                    var runNum = reader.GetInt32(0);
                    var dateStarted = reader.GetDateTime(1);
                    var dateCompleted = reader.IsDBNull(2) ? (DateTime?)null : reader.GetDateTime(2);
                    var status = reader.GetString(3);
                    return (runNum, dateStarted, dateCompleted, status);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get run info for {RunNumber}", runNumber);
            }

            return default;
        }
    }
}
