using Serilog;
using System.Data.SQLite;

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

        public RunInfoService(string connectionString, ILogger logger)
        {
            _connectionString = connectionString;
            _logger = logger;
        }

        /// <summary>
        /// Gets the next run number by querying the database for the highest existing run number.
        /// Creates a new RunInfo entry and returns the assigned run number.
        /// </summary>
        public int GetNextRunNumber(string mode = "Debug")
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();

                // Get the highest run number
                int nextRunNumber = 1;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COALESCE(MAX(RunNumber), 0) + 1 FROM RunInfo;";
                    var result = command.ExecuteScalar();
                    if (result != null && int.TryParse(result.ToString(), out int maxRun))
                    {
                        nextRunNumber = maxRun;
                    }
                }

                // Insert new run entry
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        INSERT INTO RunInfo (RunNumber, DateStarted, Status, Mode)
                        VALUES (@runNumber, @dateStarted, @status, @mode);";

                    command.Parameters.AddWithValue("@runNumber", nextRunNumber);
                    command.Parameters.AddWithValue("@dateStarted", DateTime.UtcNow);
                    command.Parameters.AddWithValue("@status", "InProgress");
                    command.Parameters.AddWithValue("@mode", mode);

                    command.ExecuteNonQuery();
                }

                _logger.Information("Created new run entry with RunNumber: {RunNumber}", nextRunNumber);
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

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE RunInfo 
                    SET DateCompleted = @dateCompleted, 
                        Status = @status, 
                        TotalReplaysProcessed = @totalReplays
                    WHERE RunNumber = @runNumber;";

                command.Parameters.AddWithValue("@dateCompleted", DateTime.UtcNow);
                command.Parameters.AddWithValue("@status", "Completed");
                command.Parameters.AddWithValue("@totalReplays", totalReplaysProcessed);
                command.Parameters.AddWithValue("@runNumber", runNumber);

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

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    UPDATE RunInfo 
                    SET DateCompleted = @dateCompleted, 
                        Status = @status, 
                        Notes = @notes
                    WHERE RunNumber = @runNumber;";

                command.Parameters.AddWithValue("@dateCompleted", DateTime.UtcNow);
                command.Parameters.AddWithValue("@status", "Failed");
                command.Parameters.AddWithValue("@notes", errorMessage ?? "Unknown error");
                command.Parameters.AddWithValue("@runNumber", runNumber);

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

                using var command = connection.CreateCommand();
                command.CommandText = @"
                    SELECT RunNumber, DateStarted, DateCompleted, Status 
                    FROM RunInfo 
                    WHERE RunNumber = @runNumber;";

                command.Parameters.AddWithValue("@runNumber", runNumber);

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
