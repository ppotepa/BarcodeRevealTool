-- Migration 019: Rename tables for consistency
-- Date: 2025-12-11
-- Description: Standardize table naming where appropriate
--              BuildOrder -> BuildOrders, UserAccount -> UserAccounts
--              Note: Debug-only LobbyFiles table (from Debug.sql) is left as-is

-- Drop any existing plural table names first (in case of previous failed migrations)
DROP TABLE IF EXISTS BuildOrders;
DROP TABLE IF EXISTS UserAccounts;

-- Rename BuildOrder to BuildOrders
ALTER TABLE BuildOrder RENAME TO BuildOrders;

-- Rename UserAccount to UserAccounts
ALTER TABLE UserAccount RENAME TO UserAccounts;

-- SQLite automatically updates indexes when tables are renamed
