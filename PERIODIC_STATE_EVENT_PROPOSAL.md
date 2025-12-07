# Periodic State Event System - Proposal

## Current Situation
- `GameEngine.MonitorGameStateAsync()` checks game state every `refreshInterval` ms
- Only fires `StateChanged` event when state **changes** (Awaiting ↔ InGame)
- Console UI refreshes only on state change
- No periodic updates happen when state is stable

## Proposed Solution

### 1. New Periodic State Event
Add a **new event** that fires **every refreshInterval** with current engine state:

```csharp
// In GameEngine.cs
public event EventHandler<PeriodicStateEventArgs>? PeriodicStateUpdate;

public class PeriodicStateEventArgs : EventArgs
{
    public ToolState CurrentState { get; set; }
    public DateTime Timestamp { get; set; }
    public ISoloGameLobby? CurrentLobby { get; set; }  // Only if InGame
    public CacheStatistics? CacheStats { get; set; }   // Database info
}
```

### 2. Where It Fires
In `MonitorGameStateAsync()`, after the refresh interval delay:

```csharp
private async Task MonitorGameStateAsync(CancellationToken cancellationToken)
{
    int stateCheckIntervalMs = Configuration?.RefreshInterval ?? 500;
    
    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            // ... existing state check logic ...
            
            // Fire periodic update event
            OnPeriodicStateUpdate(new PeriodicStateEventArgs
            {
                CurrentState = CurrentState,
                CurrentLobby = CurrentState == ToolState.InGame ? _cachedLobby : null,
                Timestamp = DateTime.UtcNow,
                CacheStats = GetCacheStatistics()
            });
            
            await Task.Delay(stateCheckIntervalMs, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _outputProvider.RenderError($"Error in monitoring loop: {ex.Message}");
            await Task.Delay(stateCheckIntervalMs, cancellationToken);
        }
    }
}
```

### 3. Console App Listeners

**In Program.cs, after getting engine:**
```csharp
var engine = serviceProvider.GetRequiredService<GameEngine>();

// Register periodic state listener
engine.PeriodicStateUpdate += (sender, args) =>
{
    outputProvider.RefreshView(args.CurrentState, args.CurrentLobby);
};

await engine.Run();
```

**In SpectreConsoleOutputProvider:**
```csharp
public void RefreshView(ToolState state, ISoloGameLobby? lobby)
{
    if (state == ToolState.InGame && lobby != null)
    {
        // Refresh lobby display (only if changed)
        RenderLobbyInfo(lobby, lobby.AdditionalData, lobby.LastBuildOrderEntry);
    }
    else if (state == ToolState.Awaiting)
    {
        // Optional: Add waiting state animations
        RenderAwaitingStateRefresh();
    }
}

public void RenderAwaitingStateRefresh()
{
    // Could add spinning animation, typing effect, etc.
    AnsiConsole.Markup(".");  // Simple dot animation
}
```

### 4. Awaiting State Effects
You could add visual/audio effects while waiting:

```csharp
engine.PeriodicStateUpdate += (sender, args) =>
{
    if (args.CurrentState == ToolState.Awaiting)
    {
        // Option A: Spinner animation
        outputProvider.RenderAwaitingStateAnimation();
        
        // Option B: Pulse/breathing effect
        outputProvider.PulseAwaitingIndicator();
        
        // Option C: Idle check (e.g., show cache sync progress)
        outputProvider.ShowCacheStatus();
    }
};
```

---

## Implementation Plan

### Phase 1: Core Event System
1. Add `PeriodicStateEventArgs` class
2. Add `PeriodicStateUpdate` event to `GameEngine`
3. Fire event in `MonitorGameStateAsync()`
4. Expose helper methods (GetCacheStatistics)

### Phase 2: Console Integration
1. Update Program.cs to wire up listener
2. Add `RefreshView()` to `IOutputProvider` interface
3. Implement in `SpectreConsoleOutputProvider`

### Phase 3: Enhanced Awaiting State
1. Add `RenderAwaitingStateAnimation()` to output provider
2. Implement spinner/animation logic
3. Add to event listener

---

## Benefits

✅ **Real-time updates** - UI refreshes every refreshInterval (not just on state change)  
✅ **Responsive gameplay** - Lobby info stays fresh  
✅ **Visual feedback** - Can add animations while waiting  
✅ **UI-agnostic** - Event-based design works for any UI  
✅ **Low overhead** - Only fires during active monitoring  
✅ **Optional effects** - Listeners can choose what to do with events  

---

## Alternative: Async Events (Optional Enhancement)

If you want async handling (e.g., for loading async data during refresh):

```csharp
// Replace EventHandler with custom delegate
public delegate Task PeriodicStateUpdateHandler(object sender, PeriodicStateEventArgs args);
public event PeriodicStateUpdateHandler? PeriodicStateUpdate;

// Fire async
OnPeriodicStateUpdate(args);
```

But standard EventHandler is simpler and sufficient for most use cases.

---

## Questions for You

1. **Awaiting state effects**: What kind of effects would you like?
   - Spinner/animation?
   - "Listening for game..." message?
   - Cache status display?
   - Sound/beep?

2. **Refresh granularity**: Should lobby info refresh **every** event or only when it changes?
   - Every interval = smooth updates but more rendering
   - Only on change = efficient but potentially stale display

3. **Additional data**: Should `PeriodicStateEventArgs` include anything else?
   - Cache statistics?
   - Uptime?
   - Player search status?

