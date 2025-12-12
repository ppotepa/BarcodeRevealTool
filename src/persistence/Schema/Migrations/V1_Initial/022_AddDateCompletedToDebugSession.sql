-- Migration 022: Add DateCompleted column to DebugSession table
-- Tracks when a debug session was completed for proper session lifecycle management

BEGIN TRANSACTION;

-- Add DateCompleted column to DebugSession table
ALTER TABLE DebugSession ADD COLUMN DateCompleted DATETIME;

-- Create index on DateCompleted for querying completed sessions
CREATE INDEX IF NOT EXISTS IDX_DebugSession_DateCompleted ON DebugSession(DateCompleted DESC);

COMMIT;
