using Serilog;
using System.Data.SQLite;
using System.Reflection;

namespace BarcodeRevealTool.Persistence.Replay.Schema
{
    /// <summary>
    /// Loads and executes SQL schema files from embedded resources.
    /// </summary>
    public static class SchemaLoader
    {
        private static readonly ILogger _logger = Log.ForContext(typeof(SchemaLoader));

        /// <summary>
        /// Load and execute a schema SQL file from embedded resources.
        /// </summary>
        public static void ExecuteSchema(SQLiteConnection connection, string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var fullResourceName = $"BarcodeRevealTool.Persistence.Schema.{resourceName}";

                using var stream = assembly.GetManifestResourceStream(fullResourceName);
                if (stream == null)
                {
                    _logger.Error("Schema resource not found: {ResourceName}", fullResourceName);
                    throw new FileNotFoundException($"Embedded resource not found: {fullResourceName}");
                }

                using var reader = new StreamReader(stream);
                var schemaSql = reader.ReadToEnd();

                // Split by semicolon and execute statements individually
                var statements = schemaSql.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                using var command = connection.CreateCommand();
                foreach (var rawStmt in statements)
                {
                    var stmt = rawStmt.Trim();
                    if (string.IsNullOrWhiteSpace(stmt))
                        continue;

                    try
                    {
                        command.CommandText = stmt;
                        command.ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        _logger.Warning(ex, "Schema statement failed (may be normal if table exists): {Statement}",
                            stmt.Substring(0, Math.Min(100, stmt.Length)));
                    }
                }

                _logger.Information("Schema executed successfully: {ResourceName}", resourceName);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to execute schema: {ResourceName}", resourceName);
                throw;
            }
        }

        /// <summary>
        /// Load and execute multiple schema SQL files.
        /// </summary>
        public static void ExecuteSchemas(SQLiteConnection connection, params string[] resourceNames)
        {
            foreach (var resourceName in resourceNames)
            {
                ExecuteSchema(connection, resourceName);
            }
        }
    }
}
