CREATE TABLE IF NOT EXISTS RunInfo (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RunNumber INTEGER NOT NULL UNIQUE,
    DateStarted DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    DateCompleted DATETIME,
    TotalReplaysProcessed INTEGER DEFAULT 0,
    Status TEXT NOT NULL DEFAULT 'InProgress', -- InProgress, Completed, Failed
    Mode TEXT NOT NULL, -- Debug, Release
    Notes TEXT
);

CREATE INDEX IF NOT EXISTS IDX_RunInfo_RunNumber ON RunInfo(RunNumber DESC);
CREATE INDEX IF NOT EXISTS IDX_RunInfo_DateStarted ON RunInfo(DateStarted DESC);
