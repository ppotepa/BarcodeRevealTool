-- Migration 021: Enhance RunInfo for daily reset tracking
-- Date: 2025-12-11
-- Description: Add DateResetAt column to track when run counter was reset for daily management

-- Add DateResetAt column to track when the daily counter resets
ALTER TABLE RunInfo ADD COLUMN DateResetAt DATETIME;

-- Update existing records to have DateResetAt = DateStarted for consistency
UPDATE RunInfo SET DateResetAt = DateStarted WHERE DateResetAt IS NULL;
