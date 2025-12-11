-- User configuration and settings tracking
CREATE TABLE IF NOT EXISTS UserConfig (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigKey TEXT NOT NULL UNIQUE,
    ConfigValue TEXT NOT NULL,
    ValueType TEXT NOT NULL DEFAULT 'String', -- String, Int, Bool, DateTime
    LastModified DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ModifiedByRunId INTEGER,
    Notes TEXT
);

-- Track configuration change history
CREATE TABLE IF NOT EXISTS ConfigHistory (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ConfigKey TEXT NOT NULL,
    OldValue TEXT,
    NewValue TEXT NOT NULL,
    ChangedAt DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    RunId INTEGER,
    FOREIGN KEY (RunId) REFERENCES RunInfo(Id)
);

CREATE INDEX IF NOT EXISTS idx_userconfig_key ON UserConfig(ConfigKey);
CREATE INDEX IF NOT EXISTS idx_userconfig_modified ON UserConfig(LastModified DESC);
CREATE INDEX IF NOT EXISTS idx_confighistory_key ON ConfigHistory(ConfigKey);
CREATE INDEX IF NOT EXISTS idx_confighistory_date ON ConfigHistory(ChangedAt DESC);
