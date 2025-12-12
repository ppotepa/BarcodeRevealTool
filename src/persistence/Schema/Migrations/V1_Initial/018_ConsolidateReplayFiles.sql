-- Migration 018: Consolidate ReplayFiles tables
-- Date: 2025-12-11
-- Description: Merge ReplayFile (M015) into ReplayFiles (M003), keep M003 schema as the standard

-- Drop the newer ReplayFile table created by migration 015 if it exists
DROP TABLE IF EXISTS ReplayFile;

-- The ReplayFiles table from migration 003 is already the standard
-- It has the better schema with Guid, DeterministicGuid, P1Id, P2Id tracking
-- This migration simply removes the duplicate ReplayFile table
