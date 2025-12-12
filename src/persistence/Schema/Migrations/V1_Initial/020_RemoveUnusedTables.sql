-- Migration 020: Remove unused tables
-- Date: 2025-12-11
-- Description: Drop DebugSessionEvent and ConfigHistory tables as they are no longer needed

-- Drop DebugSessionEvent table
DROP TABLE IF EXISTS DebugSessionEvent;

-- Drop ConfigHistory table
DROP TABLE IF EXISTS ConfigHistory;

-- These tables were created in migrations 004 and 002 respectively
-- but are being removed as part of schema simplification
