-- Cache metadata table for match and build order caching
CREATE TABLE IF NOT EXISTS Matches (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    YourTag TEXT NOT NULL,
    OpponentTag TEXT NOT NULL,
    OpponentToon TEXT,
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

-- Build orders cache table
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

-- Cache metadata for tracking sync state
CREATE TABLE IF NOT EXISTS CacheMetadata (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
