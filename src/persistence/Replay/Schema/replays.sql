-- Replay database schema for caching discovered replays
CREATE TABLE IF NOT EXISTS Replays (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReplayGuid TEXT UNIQUE NOT NULL,
    YourPlayer TEXT,
    OpponentPlayer TEXT,
    Map TEXT,
    YourRace TEXT,
    OpponentRace TEXT,
    GameDate TEXT NOT NULL,
    ReplayFilePath TEXT UNIQUE NOT NULL,
    FileHash TEXT,
    SC2ClientVersion TEXT,
    YourPlayerId TEXT,
    OpponentPlayerId TEXT,
    BuildOrderCached INTEGER DEFAULT 0,
    CachedAt TEXT,
    RunNumber INTEGER,                      -- Which run recorded this replay
    LobbyFileId INTEGER,                    -- FK to LobbyFiles (linked after lobby stored)
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL,
    FOREIGN KEY (RunNumber) REFERENCES RunInfo(RunNumber),
    FOREIGN KEY (LobbyFileId) REFERENCES LobbyFiles(Id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS idx_replays_opponent ON Replays(OpponentPlayer);
CREATE INDEX IF NOT EXISTS idx_replays_gamedate ON Replays(GameDate DESC);
CREATE INDEX IF NOT EXISTS idx_replays_filepath ON Replays(ReplayFilePath);

-- Build order entries extracted from replays
CREATE TABLE IF NOT EXISTS BuildOrderEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReplayId INTEGER NOT NULL,
    PlayerId TEXT,
    TimeSeconds INTEGER,
    Kind TEXT,
    Name TEXT,
    FOREIGN KEY (ReplayId) REFERENCES Replays(Id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_buildorder_replayid ON BuildOrderEntries(ReplayId);
