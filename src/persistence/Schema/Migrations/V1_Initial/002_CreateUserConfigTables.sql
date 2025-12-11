-- Migration 002: Create UserConfig and ConfigHistory Tables
-- Date: 2025-12-11
-- Description: Create tables for user configuration management and change tracking

CREATE TABLE IF NOT EXISTS UserConfig (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigKey TEXT NOT NULL UNIQUE,
    ConfigValue TEXT NOT NULL,
    ValueType TEXT NOT NULL DEFAULT 'String',
    LastModified DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModifiedByRunNumber INTEGER,
    IsDefault BOOLEAN NOT NULL DEFAULT 0,
    Notes TEXT,
    FOREIGN KEY (ModifiedByRunNumber) REFERENCES RunInfo(RunNumber)
);

CREATE TABLE IF NOT EXISTS ConfigHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigKey TEXT NOT NULL,
    OldValue TEXT,
    NewValue TEXT NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    RunNumber INTEGER,
    ChangeSource TEXT DEFAULT 'Manual',
    FOREIGN KEY (RunNumber) REFERENCES RunInfo(RunNumber)
);

CREATE INDEX IF NOT EXISTS idx_userconfig_key ON UserConfig(ConfigKey);
CREATE INDEX IF NOT EXISTS idx_userconfig_modified ON UserConfig(LastModified DESC);
CREATE INDEX IF NOT EXISTS idx_confighistory_key ON ConfigHistory(ConfigKey);
CREATE INDEX IF NOT EXISTS idx_confighistory_date ON ConfigHistory(ChangedAt DESC);
