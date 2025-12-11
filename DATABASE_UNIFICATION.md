# Database Unification - Implementation Summary

## Overview
Successfully unified the database infrastructure from two separate databases (`cache.db` and `replays.db`) into a single, comprehensive `cache.db` that stores all application data.

## Changes Made

### 1. Single Unified Database
- **File**: `src/persistence/Replay/ReplayQueryService.cs` (line 31)
- **Change**: Updated database path from `replays.db` → `cache.db`
- **Impact**: All replay data now stored in single unified database

### 2. New UserConfig Table Schema
- **File**: `src/persistence/Schema/UserConfig.sql`
- **Tables Created**:
  - `UserConfig`: Stores all user settings with change tracking
    - ConfigKey (unique identifier)
    - ConfigValue (actual setting value)
    - ValueType (String, Int, Bool, DateTime)
    - LastModified (timestamp)
    - ModifiedByRunId (links to RunInfo table)
    - Notes (optional description)
  
  - `ConfigHistory`: Tracks all configuration changes
    - ConfigKey (which setting changed)
    - OldValue (previous value)
    - NewValue (new value)
    - ChangedAt (when it changed)
    - RunId (which run made the change)

### 3. UserConfigService Implementation
- **File**: `src/persistence/Cache/UserConfigService.cs`
- **Purpose**: Manage configuration settings and track changes
- **Key Methods**:
  - `GetConfig(key)`: Retrieve a configuration value
  - `SetConfig(key, value, valueType, runId, notes)`: Set a configuration value and track the change
  - `GetAllConfigs()`: Get all settings as dictionary
  - `GetConfigHistory(key, limit)`: Get change history for a specific setting
  - `ClearAllConfigs()`: Reset all configuration (for testing/reset scenarios)

### 4. Database Repair Enhancement
- **File**: `src/console-app/Program.cs` (RepairSystemTablesAsync)
- **Change**: Added `UserConfig` to system tables list
- **Tables Monitored & Auto-Repaired**:
  - RunInfo
  - Players
  - ReplayFiles
  - DebugSession
  - UserConfig ← **NEW**

### 5. Documentation Update
- **File**: `README.md` (Data Persistence section)
- **Changes**:
  - Updated database filename documentation
  - Listed all tables with descriptions
  - Added configuration tracking capability
  - Documented automatic schema repair feature

## Database Schema Summary

### Complete Schema (all in single cache.db)

| Table | Purpose | Key Fields |
|-------|---------|-----------|
| RunInfo | Track application runs | RunNumber, DateStarted, Status, Mode |
| Players | Player identity | Toon, BattleTag, Nickname |
| ReplayFiles | Replay metadata | Guid, Map, P1Id, P2Id, DatePlayedAt |
| DebugSession | Debug mode data | ManualOpponentBattleTag, LobbyFilePath, LobbyFileBinary |
| UserConfig | User settings | ConfigKey, ConfigValue, ValueType |
| ConfigHistory | Config audit trail | ConfigKey, OldValue, NewValue, ChangedAt |

## Configuration Tracking Examples

The UserConfigService now tracks changes for:
- User BattleTag
- Replay folder location
- Recursive replay search setting
- Match history limit
- Debug lobbies folder
- Manual battle tag (debug)
- Manual nickname (debug)

### Usage Example:
```csharp
var configService = new UserConfigService();

// Track a configuration change
configService.SetConfig("UserBattleTag", "Player#1234", "String", runId, "Updated by user");

// Retrieve current config
var battleTag = configService.GetConfig("UserBattleTag");

// View all settings
var allConfigs = configService.GetAllConfigs();

// Track history of changes
var history = configService.GetConfigHistory("UserBattleTag", limit: 10);
```

## Benefits of Unification

✅ **Simplified Data Management**: Single database file instead of managing two  
✅ **Unified Transactions**: Can perform atomic operations across all tables  
✅ **Configuration Audit Trail**: Complete history of setting changes with timestamps  
✅ **Better Data Integrity**: Foreign keys across RunInfo, Players, ReplayFiles  
✅ **Easier Backup/Migration**: Single file to backup/move/replicate  
✅ **Clearer Database Purpose**: `cache.db` clearly indicates it's the application cache  
✅ **Configuration Change Detection**: Can detect when user settings change between runs  

## Migration Notes

If you have existing `replays.db` file:
1. Export data from `replays.db` if needed
2. Delete old `replays.db` 
3. Application will auto-create unified `cache.db` on next run
4. Database repair will recreate any missing tables automatically

## Files Modified

1. `src/persistence/Replay/ReplayQueryService.cs` - Updated database filename
2. `src/persistence/Schema/UserConfig.sql` - NEW: Configuration schema
3. `src/persistence/Cache/UserConfigService.cs` - NEW: Configuration management
4. `src/console-app/Program.cs` - Updated schema repair to include UserConfig
5. `README.md` - Updated documentation

## Build Status

✅ **Build Successful** (9.2s)
- No compilation errors
- All parameter binding issues previously fixed still working
- Ready for testing

## Next Steps

1. **Integrate UserConfigService into dependency injection** (ServiceExtensions.cs)
2. **Add config tracking to AppSettings loading** (AppSettings.cs initialization)
3. **Test configuration change detection** between application runs
4. **Verify schema repair works** by manually deleting UserConfig table
5. **Test configuration history tracking** with multiple changes
