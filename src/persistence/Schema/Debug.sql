-- Debug mode table for storing debug session metadata and optional lobby file data
-- Only used when running in DEBUG mode
CREATE TABLE IF NOT EXISTS DebugSession (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL UNIQUE,
    
    -- Manual opponent entry data (when using manual entry mode)
    ManualOpponentBattleTag TEXT,           -- e.g., "Opponent#1234"
    ManualOpponentNickname TEXT,            -- e.g., "MyOpponent"
    
    -- Lobby file source data
    LobbyFilePath TEXT,                     -- Full path to the lobby file used in this session
    LobbyFileName TEXT,                     -- Just the filename for easy reference
    LobbyFileHash TEXT,                     -- SHA256 hash of the lobby file (for verification)
    
    -- Binary lobby file storage (only if storeLobbyFiles=true in config)
    LobbyFileBinary BLOB,                   -- Raw binary content of .sc2replay file
    LobbyFileSize INTEGER,                  -- Size in bytes (for reference)
    
    -- Debug mode settings snapshot
    DebugMode TEXT NOT NULL,                -- "ManualEntry" or "LobbyFiles"
    StoreLobbyFiles BOOLEAN NOT NULL,       -- Whether binary files are stored
    
    -- Validation tracking
    IsCorrect BOOLEAN NOT NULL DEFAULT 1,   -- 0 if opponent detection matched user's own battleTag (incorrect)
    
    -- Metadata
    DateCreated DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    Notes TEXT                              -- Any additional debug notes
);

CREATE INDEX IF NOT EXISTS IDX_DebugSession_RunNumber ON DebugSession(RunNumber);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_BattleTag ON DebugSession(ManualOpponentBattleTag);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_IsCorrect ON DebugSession(IsCorrect DESC);
CREATE INDEX IF NOT EXISTS IDX_DebugSession_DateCreated ON DebugSession(DateCreated DESC);
