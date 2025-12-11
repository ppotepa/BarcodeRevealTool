CREATE TABLE IF NOT EXISTS ReplayFiles (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Guid TEXT NOT NULL UNIQUE,
    Map TEXT,
    P1Id TEXT NOT NULL,
    P2Id TEXT NOT NULL,
    Winner TEXT,
    P1Toon TEXT,
    P2Toon TEXT,
    DeterministicGuid TEXT UNIQUE,
    DatePlayedAt TEXT NOT NULL,
    ReplayFileLocation TEXT NOT NULL UNIQUE,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_replayfiles_guid ON ReplayFiles(Guid);
CREATE INDEX IF NOT EXISTS idx_replayfiles_deterministicguid ON ReplayFiles(DeterministicGuid);
CREATE INDEX IF NOT EXISTS idx_replayfiles_dateplayedat ON ReplayFiles(DatePlayedAt DESC);
CREATE INDEX IF NOT EXISTS idx_replayfiles_p1 ON ReplayFiles(P1Id);
CREATE INDEX IF NOT EXISTS idx_replayfiles_p2 ON ReplayFiles(P2Id);
CREATE INDEX IF NOT EXISTS idx_replayfiles_location ON ReplayFiles(ReplayFileLocation);
CREATE INDEX IF NOT EXISTS idx_replayfiles_winner ON ReplayFiles(Winner);
