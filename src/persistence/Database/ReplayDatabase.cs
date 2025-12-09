using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Models;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using Serilog;
using BarcodeRevealTool.Persistence.Schema;

namespace BarcodeRevealTool.Persistence.Database
{
    /// <summary>
    /// SQLite-based database for storing and retrieving replay information and build orders.
    /// Uses SqlKata for all query construction and execution.
    /// </summary>
    public class ReplayDatabase : IReplayRepository, IReplayPersistence
    {
        private readonly string _databasePath;
        private const string DatabaseFileName = "cache.db";
        private readonly string _connectionString;
        private readonly SqliteCompiler _compiler = new();
        private readonly ILogger _logger = Log.ForContext<ReplayDatabase>();

        public ReplayDatabase(string? customPath = null)
        {
            var dbPath = customPath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "_db"
            );

            Directory.CreateDirectory(dbPath);
            _databasePath = Path.Combine(dbPath, DatabaseFileName);

            var builder = new SQLiteConnectionStringBuilder
            {
                DataSource = _databasePath,
                JournalMode = SQLiteJournalModeEnum.Wal,
                CacheSize = 2000,
                Pooling = true,
                BusyTimeout = 5000
            };
            _connectionString = builder.ToString();

            _logger.Information("ReplayDatabase initialized at {DatabasePath}", _databasePath);
            InitializeDatabase();
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

        private void InitializeDatabase()
        {
            using var connection = CreateConnection();
            _logger.Information("Initializing cache database schema...");

            try
            {
                // Execute schema files for cache database - using same Players and ReplayFiles tables
                SchemaLoader.ExecuteSchemas(connection, "Players.sql", "ReplayFiles.sql");
                _logger.Information("Cache database schema initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize cache database schema");
                throw;
            }
        }

        public IReadOnlyList<MatchResult> GetRecentMatches(string yourTag, string opponentTag, int limit)
        {
            var matches = new List<MatchResult>();

            try
            {
                using var connection = CreateConnection();

                var query = new Query("Matches")
                    .Where("OpponentTag", opponentTag)
                    .OrderByDesc("GameDate")
                    .Limit(limit)
                    .Select("Map", "YourRace", "OpponentRace", "Result", "GameDate", "OpponentNickname");

                var compiled = _compiler.Compile(query);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    matches.Add(new MatchResult(
                        OpponentTag: opponentTag,
                        GameDate: DateTime.Parse(reader["GameDate"]?.ToString() ?? DateTime.UtcNow.ToString("O")),
                        Map: reader["Map"]?.ToString() ?? "Unknown",
                        YourRace: reader["YourRace"]?.ToString() ?? "Unknown",
                        OpponentRace: reader["OpponentRace"]?.ToString() ?? "Unknown",
                        YouWon: reader["Result"]?.ToString()?.Equals("WIN", StringComparison.OrdinalIgnoreCase) ?? false
                    ));
                }

                _logger.Debug("Retrieved {Count} recent matches for opponent {OpponentTag}", matches.Count, opponentTag);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve recent matches for opponent {OpponentTag}", opponentTag);
            }

            return matches;
        }

        public IReadOnlyList<BuildOrderStep> GetRecentBuildOrder(string opponentTag, int limit)
        {
            var buildOrders = new List<BuildOrderStep>();

            try
            {
                using var connection = CreateConnection();

                var query = new Query("BuildOrders")
                    .Where("OpponentTag", opponentTag)
                    .OrderByDesc("TimeSeconds")
                    .Limit(limit)
                    .Select("TimeSeconds", "Kind", "Name");

                var compiled = _compiler.Compile(query);

                using var command = connection.CreateCommand();
                command.CommandText = compiled.Sql;
                foreach (var binding in compiled.Bindings)
                {
                    command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                }

                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    buildOrders.Add(new BuildOrderStep(
                        TimeSeconds: Convert.ToDouble(reader["TimeSeconds"]),
                        Kind: reader["Kind"]?.ToString() ?? "Unknown",
                        Name: reader["Name"]?.ToString() ?? "Unknown"
                    ));
                }

                _logger.Debug("Retrieved {Count} build order steps for opponent {OpponentTag}", buildOrders.Count, opponentTag);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve build order for opponent {OpponentTag}", opponentTag);
            }

            return buildOrders;
        }

        public CacheStatistics GetCacheStatistics()
        {
            try
            {
                using var connection = CreateConnection();

                var matchesQuery = new Query("Matches").AsCount();
                var buildOrdersQuery = new Query("BuildOrders").AsCount();
                var metadataQuery = new Query("CacheMetadata")
                    .Where("Key", "LastSync")
                    .Select("Value");

                var matchesCompiled = _compiler.Compile(matchesQuery);
                var buildOrdersCompiled = _compiler.Compile(buildOrdersQuery);
                var metadataCompiled = _compiler.Compile(metadataQuery);

                int totalMatches = 0;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = matchesCompiled.Sql;
                    foreach (var binding in matchesCompiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    totalMatches = Convert.ToInt32(command.ExecuteScalar() ?? 0);
                }

                int totalBuildOrders = 0;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = buildOrdersCompiled.Sql;
                    foreach (var binding in buildOrdersCompiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    totalBuildOrders = Convert.ToInt32(command.ExecuteScalar() ?? 0);
                }

                DateTime lastSync = DateTime.MinValue;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = metadataCompiled.Sql;
                    foreach (var binding in metadataCompiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    var result = command.ExecuteScalar();
                    if (result != null && DateTime.TryParse(result.ToString(), out var parsed))
                    {
                        lastSync = parsed;
                    }
                }

                _logger.Debug("Cache statistics: {TotalMatches} matches, {TotalBuildOrders} build orders, last sync {LastSync}",
                    totalMatches, totalBuildOrders, lastSync);

                return new CacheStatistics(totalMatches, totalBuildOrders, lastSync);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to retrieve cache statistics");
            }

            return new CacheStatistics(0, 0, DateTime.MinValue);
        }

        public async Task SaveMatchAsync(MatchResult match)
        {
            try
            {
                using var connection = CreateConnection();

                var query = new Query("Matches").AsInsert(new
                {
                    YourTag = match.OpponentTag,
                    OpponentTag = match.OpponentTag,
                    Map = match.Map,
                    YourRace = match.YourRace,
                    OpponentRace = match.OpponentRace,
                    Result = match.YouWon ? "WIN" : "LOSS",
                    GameDate = match.GameDate.ToString("O"),
                    ReplayFilePath = (string?)null,
                    CreatedAt = DateTime.UtcNow.ToString("O")
                });

                var compiled = _compiler.Compile(query);

                await Task.Run(() =>
                {
                    using var command = connection.CreateCommand();
                    command.CommandText = compiled.Sql;
                    foreach (var binding in compiled.Bindings)
                    {
                        command.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    command.ExecuteNonQuery();
                });

                _logger.Debug("Saved match for opponent {OpponentTag} on {Map}", match.OpponentTag, match.Map);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save match for opponent {OpponentTag}", match.OpponentTag);
                throw;
            }
        }

        public async Task SaveBuildOrderAsync(string opponentTag, IReadOnlyList<BuildOrderStep> buildOrder)
        {
            try
            {
                using var connection = CreateConnection();

                // Delete existing build orders using SqlKata
                var deleteQuery = new Query("BuildOrders")
                    .Where("OpponentTag", opponentTag)
                    .AsDelete();

                var deleteCompiled = _compiler.Compile(deleteQuery);

                await Task.Run(() =>
                {
                    using var deleteCmd = connection.CreateCommand();
                    deleteCmd.CommandText = deleteCompiled.Sql;
                    foreach (var binding in deleteCompiled.Bindings)
                    {
                        deleteCmd.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                    }
                    deleteCmd.ExecuteNonQuery();
                });

                // Insert new build orders
                foreach (var step in buildOrder)
                {
                    var insertQuery = new Query("BuildOrders").AsInsert(new
                    {
                        OpponentTag = opponentTag,
                        TimeSeconds = step.TimeSeconds,
                        Kind = step.Kind,
                        Name = step.Name,
                        ReplayFilePath = (string?)null,
                        RecordedAt = DateTime.UtcNow.ToString("O"),
                        CreatedAt = DateTime.UtcNow.ToString("O")
                    });

                    var insertCompiled = _compiler.Compile(insertQuery);

                    await Task.Run(() =>
                    {
                        using var insertCmd = connection.CreateCommand();
                        insertCmd.CommandText = insertCompiled.Sql;
                        foreach (var binding in insertCompiled.Bindings)
                        {
                            insertCmd.Parameters.Add(new SQLiteParameter { Value = binding ?? DBNull.Value });
                        }
                        insertCmd.ExecuteNonQuery();
                    });
                }

                _logger.Debug("Saved {Count} build order steps for opponent {OpponentTag}", buildOrder.Count, opponentTag);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to save build order for opponent {OpponentTag}", opponentTag);
                throw;
            }
        }
    }
}
