-- Debug mode table for storing debug session metadata and user actions during run
-- Only used when running in DEBUG mode
CREATE TABLE IF NOT EXISTS DebugSession (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL,
    
    -- Manual opponent entry data (when using manual entry mode)
    ManualOpponentBattleTag TEXT,           -- e.g., "Opponent#1234"
    ManualOpponentNickname TEXT,            -- e.g., "MyOpponent"
    
    -- Preset battle tag if used for this run
    PresetUserBattleTag TEXT,               -- User's battle tag if run was started with preset
    
    -- Match tracking during run
    TotalMatchesPlayed INTEGER DEFAULT 0,   -- Incremented after each game
    TotalLobbiesProcessed INTEGER DEFAULT 0, -- Incremented as lobbies are detected
    
    -- Metadata
    DateStarted DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateCompleted DATETIME,
    Status TEXT NOT NULL DEFAULT 'InProgress', -- InProgress, Completed, Failed
    Notes TEXT                              -- Any additional debug notes
);

-- Table for storing individual match events during a debug run
CREATE TABLE IF NOT EXISTS DebugSessionEvents (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DebugSessionId INTEGER NOT NULL,
    EventType TEXT NOT NULL,                -- 'LobbyDetected', 'LobbyFileStored', 'MatchFinished', 'DebugAction'
    EventData TEXT,                         -- JSON or plain text data about the event
    OpponentBattleTag TEXT,                 -- Opponent involved in this event (if applicable)
    OpponentToon TEXT,                      -- Opponent toon if known
    LobbyFileName TEXT,                     -- Lobby file name if applicable
    ReplayFileName TEXT,                    -- Replay file name if applicable
    EventTime DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (DebugSessionId) REFERENCES DebugSession(Id) ON DELETE CASCADE
);

-- Table for storing binary lobby files for each match
CREATE TABLE IF NOT EXISTS LobbyFiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL,
    DebugSessionId INTEGER,                 -- FK to DebugSession if debug mode
    ReplayId INTEGER,                       -- FK to Replays (linked after replay is saved)
    MatchIndex INTEGER,                     -- Which match in this run (1, 2, 3...)
    LobbyFileName TEXT NOT NULL,            -- Original filename
    LobbyFilePath TEXT,                     -- Path where it was found
    LobbyFileHash TEXT NOT NULL,            -- SHA256 hash for deduplication
    LobbyFileBinary BLOB NOT NULL,          -- Raw binary content of lobby file
    LobbyFileSize INTEGER NOT NULL,         -- Size in bytes
    DetectedOpponentBattleTag TEXT,         -- Battle tag extracted from lobby
    DetectedOpponentToon TEXT,              -- Toon extracted from lobby
    StoredAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (RunNumber) REFERENCES RunInfo(RunNumber),
    FOREIGN KEY (DebugSessionId) REFERENCES DebugSession(Id),
    FOREIGN KEY (ReplayId) REFERENCES Replays(Id) ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS IDX_DebugSession_RunNumber ON DebugSession(RunNumber);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_DateStarted ON DebugSession(DateStarted DESC);
CREATE INDEX IF NOT EXISTS IDX_DebugSessionEvents_SessionId ON DebugSessionEvents(DebugSessionId);
CREATE INDEX IF NOT EXISTS IDX_DebugSessionEvents_EventTime ON DebugSessionEvents(EventTime DESC);
CREATE INDEX IF NOT EXISTS IDX_LobbyFiles_RunNumber ON LobbyFiles(RunNumber);
CREATE INDEX IF NOT EXISTS IDX_LobbyFiles_ReplayId ON LobbyFiles(ReplayId);
CREATE INDEX IF NOT EXISTS IDX_LobbyFiles_Hash ON LobbyFiles(LobbyFileHash);
CREATE INDEX IF NOT EXISTS IDX_LobbyFiles_OpponentTag ON LobbyFiles(DetectedOpponentBattleTag);
