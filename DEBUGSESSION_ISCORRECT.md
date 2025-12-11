# DebugSession IsCorrect Column - Logic & Usage

## Overview
Added a new `IsCorrect` boolean column to the `DebugSession` table to track whether a debug session represents a valid opponent detection or an error case.

## Database Migration
**File**: `src/persistence/Schema/Migrations/V1_Initial/006_AddDebugSessionIsCorrectColumn.sql`

The migration adds:
- `IsCorrect BOOLEAN DEFAULT 1` - Defaults to true (correct detection)
- Index on `IsCorrect` for quick filtering of bad detections

## Logic Behind IsCorrect

### What "Incorrect" Means
A debug session is marked as **incorrect (IsCorrect = 0)** when:
- **The detected opponent battleTag matches the user's own battleTag from settings**
- This indicates the system incorrectly identified the player as playing against themselves
- Typically happens when opponent detection fails or returns the wrong player

### What "Correct" Means
A debug session is marked as **correct (IsCorrect = 1)** when:
- **The detected opponent battleTag is different from the user's battleTag**
- Normal, valid detection scenario
- Default state when session is first recorded

## Implementation Details

### Default Behavior
When recording a debug session:
```csharp
// Both manual entry and lobby file modes default to IsCorrect = 1
["IsCorrect"] = 1  // Assume correct until validated
```

### Validation Method
```csharp
bool ValidateOpponentIsNotUser(int runNumber, string userBattleTag)
```

**Purpose**: Validates a recorded debug session against the user's current battleTag setting

**Logic**:
1. Retrieves the debug session by runNumber
2. Gets the opponent battleTag from the session
3. Compares with user's battleTag (case-insensitive)
4. If they match:
   - Sets `IsCorrect = 0` (incorrect detection)
   - Logs a warning about the bad detection
   - Returns `false`
5. If they differ:
   - Sets `IsCorrect = 1` (correct detection)
   - Returns `true`

**Usage**:
```csharp
var debugService = new DebugSessionService(connectionString, logger);

// After a debug session is recorded and opponent is detected:
bool isCorrect = debugService.ValidateOpponentIsNotUser(runNumber, userBattleTag);

if (!isCorrect)
{
    // Handle bad detection - opponent was same as user
    logger.Warning("Bad detection: opponent is user's own battleTag");
}
```

## Query Methods

### Get All Incorrect Sessions
```csharp
List<DebugSessionInfo> GetIncorrectSessions(
    DateTime? fromDate = null, 
    DateTime? toDate = null)
```

Returns all debug sessions where `IsCorrect = 0` (bad detections), optionally filtered by date range.

**Use Case**: Finding all instances where the system incorrectly detected the user as the opponent.

**Example**:
```csharp
// Get all incorrect detections from the last week
var incorrectSessions = debugService.GetIncorrectSessions(
    fromDate: DateTime.UtcNow.AddDays(-7)
);

foreach (var session in incorrectSessions)
{
    Console.WriteLine($"RunNumber: {session.RunNumber}, " +
                      $"Opponent: {session.ManualOpponentBattleTag}, " +
                      $"IsCorrect: {session.IsCorrect}");
}
```

## DebugSessionInfo Properties
```csharp
public class DebugSessionInfo
{
    public bool IsCorrect { get; set; } = true;  // NEW
    // ... other properties
}
```

The `IsCorrect` property is now included when retrieving debug session information via `GetDebugSession()`.

## Schema Changes Summary

### Debug.sql (Initial Schema)
```sql
CREATE TABLE IF NOT EXISTS DebugSession (
    ...
    -- Validation tracking
    IsCorrect BOOLEAN NOT NULL DEFAULT 1,   -- 0 if opponent detection matched user's own battleTag
    ...
);

CREATE INDEX IF NOT EXISTS IDX_DebugSession_IsCorrect ON DebugSession(IsCorrect DESC);
```

## Typical Workflow

1. **Record Session**: Debug session created with `IsCorrect = 1` (assumed correct)
   ```csharp
   debugService.RecordManualOpponentSession(runNumber, battleTag, nickname);
   ```

2. **Get Current Settings**: Retrieve user's battleTag from config
   ```csharp
   var userBattleTag = configService.GetConfig("UserBattleTag");
   ```

3. **Validate Session**: Check if opponent is actually a different player
   ```csharp
   bool isCorrect = debugService.ValidateOpponentIsNotUser(runNumber, userBattleTag);
   ```

4. **Review Incorrect**: Find and investigate bad detections
   ```csharp
   var incorrectSessions = debugService.GetIncorrectSessions();
   ```

## SQL Examples

### Find All Incorrect Detections
```sql
SELECT RunNumber, ManualOpponentBattleTag, DateCreated 
FROM DebugSession 
WHERE IsCorrect = 0 
ORDER BY DateCreated DESC;
```

### Count Correct vs Incorrect
```sql
SELECT 
    IsCorrect,
    COUNT(*) as SessionCount,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM DebugSession), 2) as Percentage
FROM DebugSession 
GROUP BY IsCorrect;
```

### Find Incorrect Sessions in Date Range
```sql
SELECT RunNumber, ManualOpponentBattleTag, DateCreated 
FROM DebugSession 
WHERE IsCorrect = 0 
  AND DateCreated >= '2025-12-01' 
  AND DateCreated <= '2025-12-31' 
ORDER BY DateCreated DESC;
```

## Performance Notes
- Index on `IsCorrect DESC` allows fast filtering of incorrect detections
- Comparison is case-insensitive to handle battleTag variations
- Validation is performed post-detection (non-blocking)
