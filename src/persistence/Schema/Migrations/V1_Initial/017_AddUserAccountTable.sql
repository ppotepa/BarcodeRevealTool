-- Migration 017: Add UserAccount table for Unit of Work pattern
-- Date: 2025-12-11
-- Description: Create UserAccount table for user account management

CREATE TABLE IF NOT EXISTS UserAccount (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    BattleTag TEXT,
    AccountName TEXT,
    Realm TEXT,
    Region TEXT,
    AccountId INTEGER,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT
);

CREATE INDEX IF NOT EXISTS idx_useraccount_battletag ON UserAccount(BattleTag);
CREATE INDEX IF NOT EXISTS idx_useraccount_accountid ON UserAccount(AccountId);
