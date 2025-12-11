-- Migration 013: Create DebugSessionEvent table for Unit of Work pattern
-- Date: 2025-12-11
-- Description: Create normalized DebugSessionEvent table with Unit of Work pattern
--              This replaces the old DebugSessionEvents table from migration 004

-- Create new DebugSessionEvent table with normalized schema and Unit of Work pattern
CREATE TABLE IF NOT EXISTS DebugSessionEvent (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    DebugSessionId INTEGER NOT NULL,
    EventType TEXT NOT NULL,
    EventDetails TEXT,
    OccurredAt TEXT NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UpdatedAt TEXT,
    FOREIGN KEY (DebugSessionId) REFERENCES DebugSession(Id)
);

-- Create indexes for common queries
CREATE INDEX IF NOT EXISTS idx_debugsessionevent_sessionid ON DebugSessionEvent(DebugSessionId);
CREATE INDEX IF NOT EXISTS idx_debugsessionevent_eventtype ON DebugSessionEvent(EventType);
CREATE INDEX IF NOT EXISTS idx_debugsessionevent_occurredat ON DebugSessionEvent(OccurredAt);
