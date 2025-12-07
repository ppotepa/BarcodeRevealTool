# Complete App Flow Implementation - No Redundancies

## Current Issues to Fix

1. **REDUNDANCY**: `SyncReplaysFromDiskAsync()` is called:
   - When entering InGame (to sync new replays)
   - When leaving InGame (to sync the just-played replay)
   - This scans ALL replay files, checks DB, then decodes uncached ones
   - **Problem**: On every InGame→Awaiting transition, we're scanning files unnecessarily

2. **MISSING**: When leaving InGame, we should directly add the **just-played replay** to the DB
   - Not scan the folder
   - Not decode old replays
   - Just add the one that was just recorded

3. **MISSING**: `PeriodicStateUpdate` event system (half-implemented)

4. **MISSING**: Remove `RefreshInterval` completely

---

## Correct App Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ APP START                                                       │
└─────────────────────────────────────────────────────────────────┘
         │
         ├─→ Check if cache.lock exists?
         │
         ├─ YES: Cache exists
         │   └─→ Read replays.db into memory (fast, no file scan)
         │       └─→ APP READY
         │
         └─ NO: First startup
             └─→ Scan replay folder (parallel)
                 └─→ Decode each replay (parallel)
                     └─→ Insert metadata into replays.db
                         └─→ Create cache.lock
                             └─→ APP READY

┌─────────────────────────────────────────────────────────────────┐
│ APP RUNNING (MonitorGameStateAsync Loop - 1500ms)              │
└─────────────────────────────────────────────────────────────────┘
         │
         ├─→ Every 1500ms:
         │   ├─ Check if lobby file exists
         │   ├─ Fire PeriodicStateUpdate event (always)
         │   └─ Delay 1500ms
         │
         └─→ State Change Detected?
             │
             ├─ NO: Continue loop
             │
             ├─ YES (InGame): Enter lobby detection
             │   ├─→ Parse lobby
             │   ├─→ Fire StateChanged event
             │   └─→ Load opponent stats (async)
             │
             └─ YES (Awaiting): Exit lobby detection
                 ├─→ Fire StateChanged event
                 ├─→ Save just-played replay to DB
                 │   └─→ Decode ONLY that one replay file
                 │       └─→ Insert into replays.db
                 │           └─→ Done (no folder scan)
                 └─→ Update display
```

---

## Key Changes Required

### 1. **Remove RefreshInterval (Cleanup)**

**Files:**
- `AppSettings.cs` - Remove property
- `ToolConfiguration.cs` - Remove property
- `appsettings.json` - Remove setting
- `GameEngine.cs` - Remove reference, add constant `const int StateCheckIntervalMs = 1500;`

---

### 2. **Add PeriodicStateUpdate Event (GameEngine.cs)**

**Add:**
```
- PeriodicStateEventArgs class
- public event EventHandler<PeriodicStateEventArgs>? PeriodicStateUpdate;
- protected void OnPeriodicStateUpdate(PeriodicStateEventArgs args)
```

**Fire in MonitorGameStateAsync():**
```
After every delay (1500ms), fire:
OnPeriodicStateUpdate(new PeriodicStateEventArgs 
{
    CurrentState = CurrentState,
    CurrentLobby = CurrentState == ToolState.InGame ? _cachedLobby : null,
    Timestamp = DateTime.UtcNow
})
```

---

### 3. **Add New Method: OnExitingGame() (GameEngine.cs)**

**Purpose**: When transitioning from InGame → Awaiting, save the replay that was just played

**Method flow:**
1. Detect transition InGame → Awaiting
2. Call new method `OnExitingGameAsync()`
3. This method:
   - Gets the lobby file path (which was just closed)
   - Decodes ONLY that one replay
   - Extracts metadata
   - Inserts into replays.db
   - Clears the cached lobby

**Key**: This replaces the folder scan that happens on exit

---

### 4. **Eliminate SyncReplaysFromDiskAsync() When Exiting Game**

**Current (WRONG):**
```
InGame → Awaiting:
  ├─ SyncReplaysFromDiskAsync()  ← Scans entire folder!
  └─ DisplayCurrentState()
```

**New (CORRECT):**
```
InGame → Awaiting:
  ├─ OnExitingGameAsync()  ← Saves ONLY the replay that just finished
  └─ DisplayCurrentState()
```

---

### 5. **Keep SyncReplaysFromDiskAsync() ONLY for Entering Game**

**Still needed:**
```
Awaiting → InGame:
  ├─ SyncReplaysFromDiskAsync()  ← OK: User might have played 1v1s, we need to find new replays
  ├─ ParseLobby()
  └─ DisplayCurrentState()
```

**Why**: When entering a game, user might have played before and we need to know opponent history

---

### 6. **Update IReplayService Interface**

**Add new method:**
```
Task SaveReplayTodbAsync(string replayFilePath);
```

**This method:**
- Takes ONE replay file path
- Decodes it
- Inserts into DB
- No folder scan, no checks

---

### 7. **Implement in ReplayService**

**Add new method:**
```csharp
public async Task SaveReplayToDbAsync(string replayFilePath)
{
    // Decode the single replay
    var metadata = BuildOrderReader.GetReplayMetadataFast(replayFilePath);
    
    // Insert into database
    if (metadata != null)
    {
        var database = BuildOrderReader.GetDatabase();
        if (database != null)
        {
            database.CacheMetadata(metadata);
        }
    }
}
```

---

### 8. **Update GameEngine.MonitorGameStateAsync()**

**Old logic:**
```
if (newState != CurrentState) {
    CurrentState = newState;
    
    if (CurrentState == ToolState.InGame) {
        await SyncReplaysFromDiskAsync();  ← Folder scan
        await ProcessLobbyAsync();
    } else {
        await SyncReplaysFromDiskAsync();  ← REDUNDANT! Folder scan again!
        DisplayCurrentState();
    }
}
```

**New logic:**
```
if (newState != CurrentState) {
    CurrentState = newState;
    
    if (CurrentState == ToolState.InGame) {
        await SyncReplaysFromDiskAsync();  ← OK: Find new replays for opponent history
        await ProcessLobbyAsync();
    } else {
        await OnExitingGameAsync();        ← NEW: Save just-played replay only
        DisplayCurrentState();
    }
}

// Fire periodic update every iteration (not just on state change)
OnPeriodicStateUpdate(...);
```

---

### 9. **Add UI Integration (Program.cs)**

**Wire event listeners:**
```csharp
// On state change: Full UI refresh
engine.StateChanged += (sender, args) => {
    outputProvider.HandleStateChange(args.PreviousState, args.NewState);
};

// Every 1500ms: Periodic updates (animations, etc.)
engine.PeriodicStateUpdate += (sender, args) => {
    outputProvider.HandlePeriodicUpdate(args.CurrentState, args.CurrentLobby);
};
```

---

### 10. **Update IOutputProvider**

**Add methods:**
```
void HandlePeriodicUpdate(ToolState state, ISoloGameLobby? lobby);
void RenderAwaitingStateAnimation();
```

---

## Summary of Changes

| Change | File | Impact |
|--------|------|--------|
| Remove RefreshInterval | AppSettings.cs, ToolConfiguration.cs, appsettings.json | Config cleanup |
| Add constant | GameEngine.cs | Hard-code 1500ms |
| Add events | GameEngine.cs | PeriodicStateUpdate |
| New method | GameEngine.cs | OnExitingGameAsync() |
| Update monitor loop | GameEngine.cs | Remove folder scan on exit |
| New interface method | IReplayService.cs | SaveReplayToDbAsync() |
| Implement method | ReplayService.cs | SaveReplayToDbAsync() |
| Wire events | Program.cs | Listen to StateChanged + PeriodicStateUpdate |
| Add UI methods | IOutputProvider.cs | HandlePeriodicUpdate() |
| Implement UI | SpectreConsoleOutputProvider.cs | HandlePeriodicUpdate() |

---

## Benefits

✅ **No Redundancy**: Folder only scanned:
  - Once on first startup (build cache)
  - Once on entering game (find opponent history)
  - Never on exiting game

✅ **Fast Exit**: When leaving game, only 1 replay decoded
✅ **DB Always Fresh**: Latest replay added immediately
✅ **Responsive**: Periodic events every 1500ms
✅ **Clean Code**: Clear separation of concerns

---

## Database Guarantees After Implementation

**After startup completes:**
- ✅ replays.db contains metadata for ALL historical replays (if cache exists)
- ✅ cache.lock prevents re-scanning

**While running:**
- ✅ Entering game → syncs opponent history from folder
- ✅ Exiting game → saves current replay to DB immediately
- ✅ Next startup → reads DB only, no folder scan

**Result**: Complete replay history always in database, no redundant folder scans

