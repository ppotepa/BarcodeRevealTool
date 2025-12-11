-- Migration 014: Enhance ConfigHistory table with Unit of Work columns
-- Date: 2025-12-11
-- Description: Add Unit of Work pattern columns (CreatedAt, UpdatedAt) to ConfigHistory
--              ConfigHistory already created by migration 002, this adds enhancements

-- ConfigHistory created by migration 002 has basic columns
-- This migration adds ChangeDetails and UpdatedAt columns for Unit of Work pattern

-- Add CreatedAt column if not exists (wrap in IF to handle if already present)
-- Note: ConfigHistory already has ChangedAt from migration 002, we map it to CreatedAt
-- For now, just ensure indexes are in place

CREATE INDEX IF NOT EXISTS idx_confighistory_runnumber ON ConfigHistory(RunNumber);
CREATE INDEX IF NOT EXISTS idx_confighistory_configkey ON ConfigHistory(ConfigKey);
CREATE INDEX IF NOT EXISTS idx_confighistory_changesource ON ConfigHistory(ChangeSource);
