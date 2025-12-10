# Fix for Duplicate Replay Extraction Issue

## Problem

Replay extraction was happening multiple times unnecessarily:
1. Full cache extraction during application startup
2. Incremental sync scanning immediately after
3. This was inefficient and confusing for users

The root cause was a **lock file timing issue**:
- Program.cs checked if `cache.lock` file exists BEFORE the CacheManager created it
- So the check would always be false on first run
- This triggered a full sync even if the database already had data

## Solution

### 1. **Database State Check Instead of Lock File (CacheManager.cs)**

**Added:**
```csharp
public bool IsCacheEmpty()
{
    try
    {
        var stats = GetStatistics();
        return stats.TotalMatches == 0;
    }
    catch (Exception ex)
    {
        _logger.Error(ex, "Failed to check if cache is empty");
        return true; // Assume empty if we can't check
    }
}
```

**Why:**
- More reliable than checking for a file that hasn't been created yet
- Directly checks if the database has any replay data
- Works regardless of cache.lock file state

### 2. **Simplified Cache Initialization (Program.cs)**

**Before:**
```csharp
bool lockFileExists = File.Exists(lockFilePath);  // Checked BEFORE initialization
await cacheManager.InitializeAsync();              // Creates lock file
if (!lockFileExists)                              // Stale check - always true first run
{
    await cacheManager.SyncFromDiskAsync(...);     // ALWAYS does full sync on first run
}
```

**After:**
```csharp
await cacheManager.InitializeAsync();
bool cacheIsEmpty = cacheManager.IsCacheEmpty();  // Check AFTER initialization
if (cacheIsEmpty && !string.IsNullOrEmpty(appSettings.Replays?.Folder))
{
    await cacheManager.SyncFromDiskAsync(...);     // Only if database is actually empty
}
```

**Benefits:**
- Unified logic (no more #if DEBUG / #else branches)
- More reliable state detection
- Only does full sync when cache is actually empty

### 3. **Prevent Duplicate Incremental Sync (CacheManager.cs + ReplaySyncService.cs)**

**Added to CacheManager:**
```csharp
private bool _fullSyncJustCompleted = false;

public bool WasFullSyncJustCompleted => _fullSyncJustCompleted;
public void ResetFullSyncFlag() => _fullSyncJustCompleted = false;
```

**Updated Program.cs SyncFromDiskAsync:**
```csharp
await _replayCacheService.InitializeCacheAsync();
_fullSyncJustCompleted = true;  // Mark that full sync completed
```

**Updated ReplaySyncService.InitializeAsync:**
```csharp
// Skip startup scan if a full sync was just completed (by Program.cs)
if (!_cacheManager.WasFullSyncJustCompleted)
{
    await ScanForNewReplaysAsync("startup", cancellationToken);
}
else
{
    _logger.Information("Full cache sync just completed. Skipping startup incremental scan.");
    _cacheManager.ResetFullSyncFlag();
}
```

**Benefits:**
- Avoids redundant incremental scan right after full sync
- Full sync already processes all files
- Incremental scan is only needed when monitoring for new replays during gameplay

## Execution Flow (After Fix)

### First Run (Cache Empty)
```
Program.cs InitializeCacheAsync()
  ├─ cacheManager.InitializeAsync()  (acquire lock)
  ├─ Check: cacheIsEmpty = true
  ├─ Call SyncFromDiskAsync()
  │  └─ Full extraction: scan all replays, extract metadata, insert in DB
  │  └─ Set _fullSyncJustCompleted = true
  │
GameOrchestrator.RunAsync()
  └─ replaySyncService.InitializeAsync()
     ├─ cacheManager.InitializeAsync()  (returns early, already initialized)
     ├─ Check: WasFullSyncJustCompleted = true
     └─ Skip ScanForNewReplaysAsync("startup")  ← KEY OPTIMIZATION
```

### Subsequent Runs (Cache Has Data)
```
Program.cs InitializeCacheAsync()
  ├─ cacheManager.InitializeAsync()  (acquire lock)
  ├─ Check: cacheIsEmpty = false
  └─ Skip SyncFromDiskAsync()  (database already has data)
  │
GameOrchestrator.RunAsync()
  └─ replaySyncService.InitializeAsync()
     ├─ cacheManager.InitializeAsync()  (returns early)
     ├─ Check: WasFullSyncJustCompleted = false
     └─ Run ScanForNewReplaysAsync("startup")  (quick incremental sync)
```

### During Gameplay (State Changes)
```
Opponent detected → enter InGame
  └─ SyncAsync()
     └─ ScanForNewReplaysAsync("state-change")
        └─ SyncMissingReplaysAsync()  (only process new replays on disk)
```

## Files Modified

1. **src/persistence/Cache/CacheManager.cs**
   - Added `IsCacheEmpty()` method
   - Added `WasFullSyncJustCompleted` property and `ResetFullSyncFlag()` method
   - Updated `SyncFromDiskAsync()` to set the full sync flag

2. **src/engine/Domain/Abstractions/ICacheManager.cs**
   - Added `bool IsCacheEmpty()`
   - Added `bool WasFullSyncJustCompleted { get; }`
   - Added `void ResetFullSyncFlag()`

3. **src/console-app/Program.cs**
   - Replaced platform-specific #if DEBUG/#else logic with unified flow
   - Changed from lock file check to database state check
   - Simplified InitializeCacheAsync() method

4. **src/engine/Application/Services/ReplaySyncService.cs**
   - Updated `InitializeAsync()` to check `WasFullSyncJustCompleted`
   - Skips redundant startup scan after full sync

## Testing

### Scenario 1: First Run (Empty Cache)
1. Delete `_db/cache.db` and `cache.lock`
2. Run application
3. ✅ Should see full extraction happen ONCE
4. ✅ Should NOT see another "Extracting" progress bar immediately after

### Scenario 2: Subsequent Runs (Cached Data)
1. Run application again (cache.db intact)
2. ✅ Should NOT see "Extracting" progress
3. ✅ Should see "Cache already populated with X matches"

### Scenario 3: DEBUG "Start Fresh" Option
1. Run in DEBUG mode
2. Select option 2 "Start Fresh"
3. ✅ Deletes database and lock file
4. ✅ Full extraction happens
5. ✅ Should NOT see duplicate extraction

### Scenario 4: During Gameplay
1. Opponent detected, game starts
2. ✅ Should see "Scanning... for new replays (state-change)"
3. ✅ Should be FAST (only checking for missing files, not re-extracting everything)

## Performance Impact

- **First Run**: Same as before (full extraction is necessary)
- **Subsequent Runs**: FASTER (no redundant incremental scan after startup)
- **During Gameplay**: MUCH FASTER (only processes new replays on disk)

## Configuration Notes

- Cache initialization is now platform-agnostic (no more #if DEBUG branches)
- Lock file is still created and maintained for potential multi-instance protection
- Cache.lock can be safely deleted; cache will be detected as needing refresh based on database state
