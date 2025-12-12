-- Migration 005: Create Players and Other Legacy Tables
-- Date: 2025-12-11
-- Description: Create Players table and prepare schema for future enhancements

CREATE TABLE IF NOT EXISTS Players (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Nickname TEXT,
    BattleTag TEXT NOT NULL UNIQUE,
    Toon TEXT,
    Name TEXT,
    Race TEXT,
    Rating INTEGER,
    CreatedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt DATETIME
);

CREATE INDEX IF NOT EXISTS idx_players_battletag ON Players(BattleTag);
CREATE INDEX IF NOT EXISTS idx_players_rating ON Players(Rating DESC);
