# Database Migrations - Unit of Work Pattern Implementation

## Overview

Created comprehensive database migrations to support the new Unit of Work pattern entity models. All migrations run automatically on application startup before any database operations occur.

## Migration System

**Location**: `src/persistence/Schema/Migrations/`

**Runner**: `MigrationRunner.cs`
- Discovers migrations from embedded resources (SQL files)
- Tracks execution in `__MigrationHistory` table
- Runs pending migrations automatically
- Idempotent - safe to run multiple times

## Migrations Created

### Migration 011: Add DebugSession Columns
**File**: `V1_Initial/011_AddDebugSessionColumns.sql`

Adds columns to the DebugSession table:
- `PresetUserBattleTag` - Optional preset player tag
- `TotalMatchesPlayed` - Counter for matches
- `TotalLobbiesProcessed` - Counter for lobbies
- `Status` - InProgress/Completed/Failed
- `ExitCode` - Application exit code
- `CreatedAt` - Timestamp
- `UpdatedAt` - Last update timestamp

Indexes:
- `idx_debugsession_runnumber` - Fast lookup by run number
- `idx_debugsession_status` - Query sessions by status
- `idx_debugsession_createdat` - Query by creation time

### Migration 012: Add LobbyFile Table
**File**: `V1_Initial/012_AddLobbyFileTable.sql`

Creates the LobbyFile table for storing binary lobby files:
- `RunNumber` - Associated run
- `Sha256Hash` - Deduplication key (UNIQUE with RunNumber)
- `BinaryData` - Full lobby file binary content
- `MatchIndex` - Sequential match within run
- `DetectedPlayer1` - Player 1 battle tag
- `DetectedPlayer2` - Player 2 battle tag
- `CreatedAt` / `UpdatedAt` - Timestamps

Indexes:
- `idx_lobbyfile_runnumber` - Fast lookup by run
- `idx_lobbyfile_hash` - Fast duplicate detection
- `idx_lobbyfile_matchindex` - Query by match sequence

### Migration 013: Add DebugSessionEvent Table
**File**: `V1_Initial/013_AddDebugSessionEventTable.sql`

Creates the DebugSessionEvent table for event logging:
- `DebugSessionId` - Foreign key to session
- `EventType` - LobbyDetected, MatchFinished, etc
- `EventDetails` - JSON-serialized event metadata
- `OccurredAt` - When event occurred
- `CreatedAt` / `UpdatedAt` - Timestamps

Indexes:
- `idx_debugsessionevent_sessionid` - Query events by session
- `idx_debugsessionevent_eventtype` - Filter by event type
- `idx_debugsessionevent_occurredat` - Query by time

### Migration 014: Add ConfigHistory Table
**File**: `V1_Initial/014_AddConfigHistoryTable.sql`

Creates the ConfigHistory table for configuration tracking:
- `RunNumber` - Associated run
- `ConfigKey` - Configuration setting name
- `OldValue` - Previous value
- `NewValue` - Current value
- `ChangeSource` - Startup/Manual/Detected/etc
- `ChangeDetails` - Change metadata
- `CreatedAt` / `UpdatedAt` - Timestamps

Indexes:
- `idx_confighistory_runnumber` - Query by run
- `idx_confighistory_configkey` - Query by setting name
- `idx_confighistory_changesource` - Query by change source

### Migration 015: Add ReplayFile Table
**File**: `V1_Initial/015_AddReplayFileTable.sql`

Creates the ReplayFile table for replay records:
- `YourTag` / `OpponentTag` - Player identifiers
- `OpponentToon` / `OpponentNickname` - Opponent info
- `Map` - Map name
- `YourRace` / `OpponentRace` - Race selections
- `Result` / `Winner` - Match outcome
- `GameDate` - When game was played
- `ReplayFilePath` - Path to replay file (UNIQUE)
- `Sc2ClientVersion` - SC2 version
- `YouId` / `OpponentId` - Player IDs
- `Note` - User notes
- `CreatedAt` / `UpdatedAt` - Timestamps

Indexes:
- `idx_replayfile_yourtag` - Query by your tag
- `idx_replayfile_opponenttag` - Query by opponent
- `idx_replayfile_gamedate` - Query by date
- `idx_replayfile_filepath` - Lookup by file path

### Migration 016: Add BuildOrder Table
**File**: `V1_Initial/016_AddBuildOrderTable.sql`

Creates the BuildOrder table for build order data:
- `OpponentTag` - Opponent identifier
- `OpponentNickname` - Opponent nickname
- `TimeSeconds` - Time in seconds
- `Kind` - Build action type
- `Name` - Build action name
- `ReplayFilePath` - Associated replay file
- `RecordedAt` - When recorded
- `CreatedAt` / `UpdatedAt` - Timestamps

Indexes:
- `idx_buildorder_opponenttag` - Query by opponent
- `idx_buildorder_recordedat` - Query by time
- `idx_buildorder_timeseconds` - Query by build time

### Migration 017: Add UserAccount Table
**File**: `V1_Initial/017_AddUserAccountTable.sql`

Creates the UserAccount table for user management:
- `BattleTag` - Battle.net battle tag
- `AccountName` - Account name
- `Realm` - Server realm
- `Region` - Server region
- `AccountId` - Account ID
- `CreatedAt` / `UpdatedAt` - Timestamps

Indexes:
- `idx_useraccount_battletag` - Lookup by battle tag
- `idx_useraccount_accountid` - Lookup by account ID

## Migration Execution Flow

1. **Application Startup** - `Program.cs` calls `MigrationRunner.RunAllMigrationsAsync()`
2. **Discovery** - Reads all embedded SQL files from assembly resources
3. **Check Status** - Queries `__MigrationHistory` for pending migrations
4. **Execute** - Runs pending migrations in order
5. **Record** - Logs execution in history table with timing
6. **Error Handling** - Logs failures but continues (non-blocking)

## Features

✅ **Idempotent** - Safe to run multiple times
✅ **Ordered Execution** - Migrations run in numeric order (011, 012, 013...)
✅ **Tracking** - All migrations recorded in `__MigrationHistory`
✅ **Error Resilient** - Non-blocking - application continues if migration fails
✅ **Automatic** - Runs on every application startup
✅ **Embedded** - SQL files packaged in assembly
✅ **Indexed** - All important columns are indexed
✅ **Timestamped** - All tables have CreatedAt/UpdatedAt

## Project File Configuration

Updated `BarcodeRevealTool.Persistence.csproj` to embed migration SQL files:

```xml
<ItemGroup>
  <EmbeddedResource Include="Schema\*.sql" />
  <EmbeddedResource Include="Schema\Migrations\**\*.sql" />
</ItemGroup>
```

## Usage

No special action needed - migrations run automatically on startup:

```csharp
// In Program.cs
var migrationRunner = new MigrationRunner(connectionString);
var migrationResult = await migrationRunner.RunAllMigrationsAsync();
```

## Migration History Tracking

The `__MigrationHistory` table tracks:
- `MigrationName` - Name of migration
- `Version` - Version string (e.g., "V1_Initial")
- `ExecutedAt` - When executed
- `ExecutionTimeMs` - How long it took
- `Status` - Success or Failed
- `ErrorMessage` - Error details if failed

## Adding New Migrations

To add a new migration:

1. Create a new SQL file in `Schema/Migrations/V1_Initial/` with naming: `0XX_DescriptiveTitle.sql`
2. Write idempotent SQL (use `CREATE TABLE IF NOT EXISTS`, `ALTER TABLE ADD COLUMN IF NOT EXISTS`)
3. Build the project - SQL files are automatically embedded
4. Run application - new migration executes automatically

## Database Constraints

- All entity tables have `Id` (PRIMARY KEY AUTOINCREMENT)
- All entity tables have `CreatedAt` (DEFAULT CURRENT_TIMESTAMP)
- All entity tables have `UpdatedAt` (nullable)
- Foreign keys defined for relationships (DebugSessionEvent.DebugSessionId)
- Unique constraints for deduplication (LobbyFile.Sha256Hash + RunNumber)
- Unique constraints for file paths (ReplayFile.ReplayFilePath)

## Status

✅ **All Migrations Created** - 7 migrations (011-017)  
✅ **All Migrations Embedded** - SQL files packaged in assembly  
✅ **Migration Runner Active** - Executes on startup  
✅ **Build Successful** - 0 Warnings, 0 Errors

The database schema is now fully aligned with the Unit of Work entity models!
