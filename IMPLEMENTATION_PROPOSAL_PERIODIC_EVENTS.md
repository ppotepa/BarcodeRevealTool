# Periodic State Event System - Complete Implementation Proposal

## Overview
Remove configurable `refreshInterval` and implement a hard-coded 1500ms monitoring loop with event-based state updates.

---

## Changes Required

### 1. **Remove RefreshInterval Configuration**

#### Files to modify:
- `src/engine/Config/AppSettings.cs`
  - Remove `public int RefreshInterval { get; set; } = 500;`
  
- `src/engine/Config/ToolConfiguration.cs`
  - Remove `public int RefreshInterval { get; set; }`

- `src/console-app/appsettings.json`
  - Remove `"RefreshInterval": 1500` from barcodeReveal section

#### Why:
- No longer needed - interval is hard-coded at 1500ms in the engine
- Simplifies configuration and eliminates confusion
- Locks the monitoring loop to a fixed, tested interval

---

### 2. **Create PeriodicStateEventArgs Class**

#### New class in: `src/engine/GameEngine.cs` (at end of file, after ToolStateChangedEventArgs)

```
PeriodicStateEventArgs : EventArgs
├── CurrentState: ToolState
├── CurrentLobby: ISoloGameLobby? (null if not InGame)
├── Timestamp: DateTime
```

Purpose:
- Carries state information for periodic updates
- Fired every 1500ms regardless of state changes
- Different from StateChanged (which only fires on transition)

---

### 3. **Update GameEngine Monitoring Loop**

#### In: `src/engine/GameEngine.cs`

**Changes:**
1. Add constant: `private const int StateCheckIntervalMs = 1500;`
2. Add event: `public event EventHandler<PeriodicStateEventArgs>? PeriodicStateUpdate;`
3. Add method: `protected virtual void OnPeriodicStateUpdate(PeriodicStateEventArgs args)`
4. Update `MonitorGameStateAsync()`:
   - Replace `int stateCheckIntervalMs = Configuration?.RefreshInterval ?? 500;` with constant
   - Remove `lastRefreshTime` variable (no longer needed)
   - Fire `OnPeriodicStateUpdate()` after every delay, regardless of state change
   - Keep state change detection and StateChanged event as-is

**Flow in MonitorGameStateAsync:**
```
Loop:
  ├─ Check lobby file exists
  ├─ If state changed:
  │  └─ Trigger StateChanged event
  │     └─ Update UI (full refresh)
  ├─ Trigger PeriodicStateUpdate event (always)
  │  └─ Allow periodic updates (animations, counters, etc.)
  └─ Delay 1500ms
```

---

### 4. **Add Event to IOutputProvider Interface**

#### In: `src/engine/Abstractions/IOutputProvider.cs`

**Add method signatures:**
```
void RefreshView(ToolState state, ISoloGameLobby? lobby);
void RenderAwaitingStateAnimation();
void RenderAwaitingStateRefresh();
```

Purpose:
- Console app can respond to periodic updates
- Different from RenderAwaitingState() - called repeatedly
- Allows smooth animations/visual feedback

---

### 5. **Implement Events in SpectreConsoleOutputProvider**

#### In: `src/console-app/ui/SpectreConsoleOutputProvider.cs`

**Implement new methods:**
- `RefreshView(ToolState state, ISoloGameLobby? lobby)` 
  - Updates current display without clearing
  - Called on periodic events
  
- `RenderAwaitingStateAnimation()`
  - Called every 1500ms while awaiting
  - Could render spinner/pulse effect
  - Examples:
    - Rotating character (|/-\)
    - Dot animation (., .., ...)
    - Pulsing color effect
    - "Listening..." with cursor blink

- `RenderAwaitingStateRefresh()`
  - Alternative lightweight update

---

### 6. **Wire Events in Console App**

#### In: `src/console-app/Program.cs`

**After getting engine, add listeners:**
```
engine.StateChanged += (sender, args) => 
    // Full UI refresh on state change
    
engine.PeriodicStateUpdate += (sender, args) =>
    // Lightweight periodic updates
```

Flow:
1. `StateChanged` event → Full clear and re-render (InGame↔Awaiting)
2. `PeriodicStateUpdate` event → Lightweight updates/animations
3. Both handlers receive state info and can act accordingly

---

## Summary of Changes

| File | Change | Impact |
|------|--------|--------|
| `AppSettings.cs` | Remove RefreshInterval property | Configuration simplified |
| `ToolConfiguration.cs` | Remove RefreshInterval property | Configuration simplified |
| `appsettings.json` | Remove RefreshInterval setting | User config simplified |
| `GameEngine.cs` | Add constant, events, and update monitoring loop | Core functionality - periodic updates every 1500ms |
| `IOutputProvider.cs` | Add 3 new method signatures | Defines UI update contract |
| `SpectreConsoleOutputProvider.cs` | Implement 3 new methods | Console UI handles periodic updates |
| `Program.cs` | Wire up event listeners | Connects events to UI |

---

## Event Flow Diagram

```
MonitorGameStateAsync() Loop (1500ms)
│
├─ Check lobby file state
│
├─ State Changed? (Awaiting ↔ InGame)
│  ├─ YES: CurrentState = newState
│  │       └─ OnStateChanged() 
│  │           └─ engine.StateChanged event
│  │               └─ Listener: Full UI refresh
│  │                   └─ Clear screen, render complete state
│  │
│  └─ NO: Continue
│
├─ OnPeriodicStateUpdate() (always fires)
│   └─ engine.PeriodicStateUpdate event
│       └─ Listener A: RefreshView() - update existing display
│       └─ Listener B: RenderAwaitingStateAnimation() - animations while waiting
│       └─ Listener C: Custom behaviors
│
└─ Delay 1500ms
```

---

## Benefits

✅ **Simplified Configuration** - One less setting to configure  
✅ **Consistent Monitoring** - Fixed 1500ms interval, no variability  
✅ **Event-Driven UI** - Clean separation of engine and UI  
✅ **Periodic Updates** - Can animate/refresh without state change  
✅ **Responsive Feel** - Smooth visual feedback every 1500ms  
✅ **Awaiting Effects** - Can show spinner, pulse, status while waiting  
✅ **No UI Blocking** - Events fired asynchronously to background tasks  

---

## Implementation Order

1. Remove RefreshInterval from configs (AppSettings, ToolConfiguration, appsettings.json)
2. Add constant and event classes to GameEngine
3. Update MonitorGameStateAsync() logic
4. Add method signatures to IOutputProvider
5. Implement methods in SpectreConsoleOutputProvider
6. Wire event listeners in Program.cs

---

## Considerations

- **Backward Compatibility**: Config files will need updating (remove RefreshInterval)
- **Performance**: 1500ms interval is reasonable and tested
- **UI Responsiveness**: Two-tier event system (StateChanged for major, PeriodicStateUpdate for minor)
- **Animation Smoothness**: 1500ms allows for ~30-frame animations if desired
- **No Breaking Changes**: Existing StateChanged event remains unchanged

