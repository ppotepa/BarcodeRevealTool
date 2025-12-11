-- Migration 000: Initialize Migration History Tracking
-- Date: 2025-12-11
-- Description: Create the migration history table itself (executed by MigrationRunner directly)
-- Note: This migration is actually handled by MigrationRunner.InitializeMigrationTracking()
-- This file exists for documentation purposes and ensures complete migration history

-- The __MigrationHistory table is created by MigrationRunner before migrations run
-- This ensures we can track which migrations have been executed
-- See MigrationRunner.cs InitializeMigrationTracking() method
