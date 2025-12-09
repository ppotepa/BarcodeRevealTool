namespace BarcodeRevealTool.Persistence.Replay.Schema
{
    /// <summary>
    /// SQL schema definitions for replay cache database
    /// </summary>
    public static class DatabaseSchema
    {
        public const string CreateMatchesTable = @"
CREATE TABLE IF NOT EXISTS Matches (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    YourTag TEXT NOT NULL,
    OpponentTag TEXT NOT NULL,
    OpponentNickname TEXT,
    Map TEXT,
    YourRace TEXT,
    OpponentRace TEXT,
    Result TEXT,
    GameDate TEXT NOT NULL,
    ReplayFilePath TEXT,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_matches_opponent ON Matches(OpponentTag);
CREATE INDEX IF NOT EXISTS idx_matches_gamedate ON Matches(GameDate DESC);
";

        public const string CreateBuildOrdersTable = @"
CREATE TABLE IF NOT EXISTS BuildOrders (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    OpponentTag TEXT NOT NULL,
    OpponentNickname TEXT,
    TimeSeconds INTEGER NOT NULL,
    Kind TEXT,
    Name TEXT NOT NULL,
    ReplayFilePath TEXT,
    RecordedAt TEXT NOT NULL,
    CreatedAt TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS idx_buildorders_opponent ON BuildOrders(OpponentTag);
CREATE INDEX IF NOT EXISTS idx_buildorders_time ON BuildOrders(TimeSeconds);
";

        public const string CreateCacheMetadataTable = @"
CREATE TABLE IF NOT EXISTS CacheMetadata (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
";

        public const string CreateAllTables = CreateMatchesTable + "\n" + CreateBuildOrdersTable + "\n" + CreateCacheMetadataTable;

        public static class Queries
        {
            public const string GetRecentMatches = @"
SELECT Id, YourTag, OpponentTag, OpponentNickname, Map, YourRace, OpponentRace, Result, GameDate, ReplayFilePath
FROM Matches
WHERE OpponentTag = @opponentTag
ORDER BY GameDate DESC
LIMIT @limit
";

            public const string GetRecentBuildOrders = @"
SELECT Id, OpponentTag, OpponentNickname, TimeSeconds, Kind, Name, ReplayFilePath, RecordedAt
FROM BuildOrders
WHERE OpponentTag = @opponentTag
ORDER BY TimeSeconds DESC
LIMIT @limit
";

            public const string InsertMatch = @"
INSERT INTO Matches (YourTag, OpponentTag, OpponentNickname, Map, YourRace, OpponentRace, Result, GameDate, ReplayFilePath, CreatedAt)
VALUES (@yourTag, @opponentTag, @opponentNickname, @map, @yourRace, @opponentRace, @result, @gameDate, @replayFilePath, @createdAt)
";

            public const string DeleteBuildOrdersByOpponent = @"
DELETE FROM BuildOrders WHERE OpponentTag = @opponentTag
";

            public const string InsertBuildOrder = @"
INSERT INTO BuildOrders (OpponentTag, OpponentNickname, TimeSeconds, Kind, Name, ReplayFilePath, RecordedAt, CreatedAt)
VALUES (@opponentTag, @opponentNickname, @timeSeconds, @kind, @name, @replayFilePath, @recordedAt, @createdAt)
";

            public const string GetCacheStatistics = @"
SELECT 
    (SELECT COUNT(*) FROM Matches) as TotalMatches,
    (SELECT COUNT(*) FROM BuildOrders) as TotalBuildOrders,
    (SELECT Value FROM CacheMetadata WHERE Key = 'LastSync') as LastSync
";

            public const string UpdateMetadata = @"
INSERT OR REPLACE INTO CacheMetadata (Key, Value, UpdatedAt)
VALUES (@key, @value, @updatedAt)
";

            public const string GetMetadata = @"
SELECT Value FROM CacheMetadata WHERE Key = @key
";
        }
    }
}
