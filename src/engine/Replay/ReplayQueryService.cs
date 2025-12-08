using System.Data.SQLite;
using SqlKata;
using SqlKata.Compilers;
using SqlKata.Execution;
using BarcodeRevealTool.Replay;

namespace BarcodeRevealTool.Engine.Replay
{
    /// <summary>
    /// SqlKata-based query service for replay database operations.
    /// All database access goes through SqlKata for consistency and query building.
    /// </summary>
    public interface IReplayQueryService
    {
        long AddOrUpdateReplay(string player1, string player2, string map, string race1, string race2,
            DateTime gameDate, string replayFilePath, string? sc2ClientVersion = null,
            string? player1Id = null, string? player2Id = null);

        void StoreBuildOrderEntries(long replayId, Queue<BuildOrderEntry> buildOrderEntries);

        ReplayRecord? GetReplayByFilePath(string filePath);
        ReplayRecord? GetReplayByGuid(string replayGuid);
        Queue<BuildOrderEntry> GetBuildOrderEntries(long replayId);
        List<ReplayRecord> GetReplaysWithPlayer(string playerName);
        List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10);
        List<ReplayRecord> GetAllCachedReplays();
        List<string> GetMissingReplayFiles(string[] allReplayFilesOnDisk);
        (int Total, int WithBuildOrder) GetDatabaseStats();
        void CacheMetadata(ReplayMetadata metadata);
        (int Total, int WithPlayers) GetCacheStats();

        /// <summary>
        /// Get the most recent replay ID for an opponent matchup.
        /// Used for lazy-loading opponent build order on game start.
        /// </summary>
        long? GetMostRecentOpponentReplayId(string yourPlayerName, string opponentName);

        /// <summary>
        /// Get the next replay ID in sequence (chronologically forward) for same opponent.
        /// Used for hotkey navigation through opponent history.
        /// </summary>
        long? GetNextOpponentReplayId(string yourPlayerName, string opponentName, DateTime currentReplayDate);

        /// <summary>
        /// Get the previous replay ID in sequence (chronologically backward) for same opponent.
        /// Used for hotkey navigation through opponent history.
        /// </summary>
        long? GetPreviousOpponentReplayId(string yourPlayerName, string opponentName, DateTime currentReplayDate);

        /// <summary>
        /// Get replay info by ID (for navigation display).
        /// </summary>
        ReplayRecord? GetReplayById(long replayId);
    }
    public class ReplayQueryService : IReplayQueryService
    {
        private readonly string _databasePath;

        public ReplayQueryService(string? customPath = null)
        {
            var dbPath = customPath ?? Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "_db"
            );

            Directory.CreateDirectory(dbPath);
            _databasePath = Path.Combine(dbPath, "replays.db");
        }

        private QueryFactory CreateQueryFactory()
        {
            var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
            var compiler = new SqliteCompiler();
            return new QueryFactory(connection, compiler);
        }

        public long AddOrUpdateReplay(string player1, string player2, string map, string race1, string race2,
            DateTime gameDate, string replayFilePath, string? sc2ClientVersion = null,
            string? player1Id = null, string? player2Id = null)
        {
            try
            {
                var replayGuid = ReplayMetadata.ComputeDeterministicGuid(
                    Path.GetFileName(replayFilePath),
                    gameDate
                );

                using var queryFactory = CreateQueryFactory();

                // Check if replay already exists
                var existing = queryFactory.Query("Replays")
                    .Where("ReplayGuid", replayGuid.ToString())
                    .FirstOrDefault<dynamic>();

                if (existing != null)
                {
                    return Convert.ToInt64(existing.Id);
                }

                // Insert new replay
                var fileHash = ComputeFileHash(replayFilePath);
                var now = DateTime.UtcNow.ToString("O");

                var result = queryFactory.Query("Replays").InsertGetId<long>(new
                {
                    ReplayGuid = replayGuid.ToString(),
                    Player1 = player1,
                    Player2 = player2,
                    Player1Id = player1Id,
                    Player2Id = player2Id,
                    Map = map,
                    Race1 = race1,
                    Race2 = race2,
                    GameDate = gameDate.ToString("O"),
                    ReplayFilePath = replayFilePath,
                    FileHash = fileHash,
                    SC2ClientVersion = sc2ClientVersion,
                    CreatedAt = now,
                    UpdatedAt = now
                });

                return result;
            }
            catch (Exception ex)
            {
                // Log error appropriately
                return 0;
            }
        }

        public void StoreBuildOrderEntries(long replayId, Queue<BuildOrderEntry> buildOrderEntries)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                // Delete existing entries
                queryFactory.Query("BuildOrderEntries")
                    .Where("ReplayId", replayId)
                    .Delete();

                // Insert new entries
                var entries = buildOrderEntries.Select(entry => new
                {
                    ReplayId = replayId,
                    PlayerId = entry.PlayerId,
                    TimeSeconds = entry.TimeSeconds,
                    Kind = entry.Kind,
                    Name = entry.Name
                }).ToList();

                if (entries.Any())
                {
                    queryFactory.Query("BuildOrderEntries").Insert(entries);
                }

                // Mark as cached
                queryFactory.Query("Replays")
                    .Where("Id", replayId)
                    .Update(new { BuildOrderCached = 1, CachedAt = DateTime.UtcNow.ToString("O") });
            }
            catch (Exception ex)
            {
                // Log error appropriately
            }
        }

        public ReplayRecord? GetReplayByFilePath(string filePath)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var replay = queryFactory.Query("Replays")
                    .Where("ReplayFilePath", filePath)
                    .FirstOrDefault<dynamic>();

                return MapToReplayRecord(replay);
            }
            catch
            {
                return null;
            }
        }

        public ReplayRecord? GetReplayByGuid(string replayGuid)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var replay = queryFactory.Query("Replays")
                    .Where("ReplayGuid", replayGuid)
                    .FirstOrDefault<dynamic>();

                return MapToReplayRecord(replay);
            }
            catch
            {
                return null;
            }
        }

        public Queue<BuildOrderEntry> GetBuildOrderEntries(long replayId)
        {
            var entries = new Queue<BuildOrderEntry>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var dbEntries = queryFactory.Query("BuildOrderEntries")
                    .Where("ReplayId", replayId)
                    .OrderBy("TimeSeconds")
                    .Get<dynamic>();

                foreach (var entry in dbEntries)
                {
                    entries.Enqueue(new BuildOrderEntry(
                        PlayerId: Convert.ToInt32(entry.PlayerId),
                        TimeSeconds: Convert.ToDouble(entry.TimeSeconds),
                        Kind: entry.Kind.ToString() ?? string.Empty,
                        Name: entry.Name.ToString() ?? string.Empty
                    ));
                }
            }
            catch
            {
                // Return empty queue on error
            }

            return entries;
        }

        public List<ReplayRecord> GetReplaysWithPlayer(string playerName)
        {
            var replays = new List<ReplayRecord>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var playerPattern = $"%{playerName}%";
                var dbReplays = queryFactory.Query("Replays")
                    .Where(query => query
                        .Where("Player1", "LIKE", playerPattern)
                        .OrWhere("Player2", "LIKE", playerPattern))
                    .OrderByDesc("GameDate")
                    .Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    replays.Add(MapToReplayRecord(replay)!);
                }
            }
            catch
            {
                // Return empty list on error
            }

            return replays;
        }

        public List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName)>
            GetOpponentMatchHistory(string yourPlayerName, string opponentName, int limit = 10)
        {
            var history = new List<(string, DateTime, string, string, string, string)>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var yourPattern = $"%{yourPlayerName}%";
                var opponentPattern = $"%{opponentName}%";

                var dbReplays = queryFactory.Query("Replays")
                    .Where(query => query
                        .Where(q => q
                            .Where("Player1", "LIKE", yourPattern)
                            .Where("Player2", "LIKE", opponentPattern))
                        .OrWhere(q => q
                            .Where("Player2", "LIKE", yourPattern)
                            .Where("Player1", "LIKE", opponentPattern)))
                    .OrderByDesc("GameDate")
                    .Limit(limit)
                    .Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    var player1 = replay.Player1.ToString() ?? string.Empty;
                    var player2 = replay.Player2.ToString() ?? string.Empty;
                    var race1 = replay.Race1.ToString() ?? string.Empty;
                    var race2 = replay.Race2.ToString() ?? string.Empty;
                    var gameDate = DateTime.Parse(replay.GameDate.ToString() ?? DateTime.MinValue.ToString("O"));
                    var map = replay.Map.ToString() ?? string.Empty;
                    var replayPath = replay.ReplayFilePath.ToString() ?? string.Empty;

                    string yourRace, opponentRaceInMatch, opponentInMatch;

                    if (player1.Contains(yourPlayerName))
                    {
                        yourRace = race1;
                        opponentRaceInMatch = race2;
                        opponentInMatch = player2;
                    }
                    else
                    {
                        yourRace = race2;
                        opponentRaceInMatch = race1;
                        opponentInMatch = player1;
                    }

                    var replayFileName = Path.GetFileName(replayPath);
                    history.Add((opponentInMatch, gameDate, map, yourRace, opponentRaceInMatch, replayFileName));
                }
            }
            catch
            {
                // Return empty list on error
            }

            return history;
        }

        public List<ReplayRecord> GetAllCachedReplays()
        {
            var replays = new List<ReplayRecord>();

            try
            {
                using var queryFactory = CreateQueryFactory();

                var dbReplays = queryFactory.Query("Replays")
                    .Select("Id", "ReplayFilePath")
                    .OrderByDesc("CreatedAt")
                    .Get<dynamic>();

                foreach (var replay in dbReplays)
                {
                    replays.Add(new ReplayRecord
                    {
                        Id = Convert.ToInt64(replay.Id),
                        ReplayFilePath = replay.ReplayFilePath.ToString() ?? string.Empty
                    });
                }
            }
            catch
            {
                // Return empty list on error
            }

            return replays;
        }

        public List<string> GetMissingReplayFiles(string[] allReplayFilesOnDisk)
        {
            var missingFiles = new List<string>();

            if (allReplayFilesOnDisk.Length == 0)
                return missingFiles;

            try
            {
                using var queryFactory = CreateQueryFactory();

                var cachedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var results = queryFactory.Query("Replays")
                    .Select("ReplayFilePath")
                    .Get<dynamic>();

                foreach (var result in results)
                {
                    var path = result.ReplayFilePath?.ToString();
                    if (!string.IsNullOrEmpty(path))
                    {
                        cachedPaths.Add(path);
                    }
                }

                foreach (var filePath in allReplayFilesOnDisk)
                {
                    if (!cachedPaths.Contains(filePath))
                    {
                        missingFiles.Add(filePath);
                    }
                }
            }
            catch
            {
                // Fallback: return all files if query fails (safer to re-process than skip)
                return new List<string>(allReplayFilesOnDisk);
            }

            return missingFiles;
        }

        public (int Total, int WithBuildOrder) GetDatabaseStats()
        {
            try
            {
                using var connection = new SQLiteConnection($"Data Source={_databasePath};Version=3;");
                connection.Open();

                // Get total count
                using var totalCmd = connection.CreateCommand();
                totalCmd.CommandText = "SELECT COUNT(*) FROM Replays";
                var total = (long?)totalCmd.ExecuteScalar() ?? 0;

                // Get count with build order cached
                using var cachedCmd = connection.CreateCommand();
                cachedCmd.CommandText = "SELECT COUNT(*) FROM Replays WHERE BuildOrderCached = 1";
                var withBuildOrder = (long?)cachedCmd.ExecuteScalar() ?? 0;

                return ((int)total, (int)withBuildOrder);
            }
            catch
            {
                return (0, 0);
            }
        }

        public void CacheMetadata(ReplayMetadata metadata)
        {
            try
            {
                // If we already have this replay by file path, skip it (idempotent)
                var existingReplay = GetReplayByFilePath(metadata.FilePath);
                if (existingReplay != null)
                {
                    return;
                }

                // Extract player data
                string player1 = string.Empty, player2 = string.Empty;
                string race1 = string.Empty, race2 = string.Empty;
                string? player1Id = null, player2Id = null;

                if (metadata.Players.Count > 0)
                {
                    player1 = metadata.Players[0].Name;
                    race1 = metadata.Players[0].Race;
                    player1Id = metadata.Players[0].PlayerId;
                }

                if (metadata.Players.Count > 1)
                {
                    player2 = metadata.Players[1].Name;
                    race2 = metadata.Players[1].Race;
                    player2Id = metadata.Players[1].PlayerId;
                }

                var map = !string.IsNullOrEmpty(metadata.Map) ? metadata.Map : "Unknown";
                var gameDate = metadata.GameDate;
                var sc2Version = metadata.SC2ClientVersion;

                AddOrUpdateReplay(player1, player2, map, race1, race2, gameDate,
                    metadata.FilePath, sc2Version, player1Id, player2Id);
            }
            catch (Exception ex)
            {
                // Log error appropriately
            }
        }

        public (int Total, int WithPlayers) GetCacheStats()
        {
            var (total, withBuildOrder) = GetDatabaseStats();
            return (total, withBuildOrder);
        }

        private ReplayRecord? MapToReplayRecord(dynamic? replay)
        {
            if (replay == null)
                return null;

            return new ReplayRecord
            {
                Id = Convert.ToInt64(replay.Id),
                Player1 = replay.Player1?.ToString() ?? string.Empty,
                Player2 = replay.Player2?.ToString() ?? string.Empty,
                Player1Id = replay.Player1Id?.ToString(),
                Player2Id = replay.Player2Id?.ToString(),
                Map = replay.Map?.ToString() ?? string.Empty,
                Race1 = replay.Race1?.ToString() ?? string.Empty,
                Race2 = replay.Race2?.ToString() ?? string.Empty,
                GameDate = DateTime.Parse(replay.GameDate?.ToString() ?? DateTime.MinValue.ToString("O")),
                ReplayFilePath = replay.ReplayFilePath?.ToString() ?? string.Empty,
                BuildOrderCached = Convert.ToInt32(replay.BuildOrderCached ?? 0) == 1,
                CachedAt = replay.CachedAt != null && replay.CachedAt != DBNull.Value
                    ? DateTime.Parse(replay.CachedAt.ToString())
                    : null
            };
        }

        private static string ComputeFileHash(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                var hash = $"{fileInfo.Length}";

                using var file = File.OpenRead(filePath);
                byte[] buffer = new byte[1024];

                file.Read(buffer, 0, 1024);
                hash += BitConverter.ToString(buffer, 0, Math.Min(32, buffer.Length));

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

        public long? GetMostRecentOpponentReplayId(string yourPlayerName, string opponentName)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var yourPattern = $"%{yourPlayerName}%";
                var opponentPattern = $"%{opponentName}%";

                var replayId = queryFactory.Query("Replays")
                    .Where(query => query
                        .Where(q => q
                            .Where("Player1", "LIKE", yourPattern)
                            .Where("Player2", "LIKE", opponentPattern))
                        .OrWhere(q => q
                            .Where("Player2", "LIKE", yourPattern)
                            .Where("Player1", "LIKE", opponentPattern)))
                    .OrderByDesc("GameDate")
                    .Limit(1)
                    .FirstOrDefault<dynamic>();

                return replayId != null ? (long?)Convert.ToInt64(replayId.Id) : null;
            }
            catch
            {
                return null;
            }
        }

        public long? GetNextOpponentReplayId(string yourPlayerName, string opponentName, DateTime currentReplayDate)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var yourPattern = $"%{yourPlayerName}%";
                var opponentPattern = $"%{opponentName}%";

                var replayId = queryFactory.Query("Replays")
                    .Where(query => query
                        .Where(q => q
                            .Where("Player1", "LIKE", yourPattern)
                            .Where("Player2", "LIKE", opponentPattern))
                        .OrWhere(q => q
                            .Where("Player2", "LIKE", yourPattern)
                            .Where("Player1", "LIKE", opponentPattern)))
                    .Where("GameDate", ">", currentReplayDate.ToString("O"))
                    .OrderBy("GameDate")
                    .Limit(1)
                    .FirstOrDefault<dynamic>();

                return replayId != null ? (long?)Convert.ToInt64(replayId.Id) : null;
            }
            catch
            {
                return null;
            }
        }

        public long? GetPreviousOpponentReplayId(string yourPlayerName, string opponentName, DateTime currentReplayDate)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var yourPattern = $"%{yourPlayerName}%";
                var opponentPattern = $"%{opponentName}%";

                var replayId = queryFactory.Query("Replays")
                    .Where(query => query
                        .Where(q => q
                            .Where("Player1", "LIKE", yourPattern)
                            .Where("Player2", "LIKE", opponentPattern))
                        .OrWhere(q => q
                            .Where("Player2", "LIKE", yourPattern)
                            .Where("Player1", "LIKE", opponentPattern)))
                    .Where("GameDate", "<", currentReplayDate.ToString("O"))
                    .OrderByDesc("GameDate")
                    .Limit(1)
                    .FirstOrDefault<dynamic>();

                return replayId != null ? (long?)Convert.ToInt64(replayId.Id) : null;
            }
            catch
            {
                return null;
            }
        }

        public ReplayRecord? GetReplayById(long replayId)
        {
            try
            {
                using var queryFactory = CreateQueryFactory();

                var replay = queryFactory.Query("Replays")
                    .Where("Id", replayId)
                    .FirstOrDefault<dynamic>();

                return MapToReplayRecord(replay);
            }
            catch
            {
                return null;
            }
        }
    }
}
