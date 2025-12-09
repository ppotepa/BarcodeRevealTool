# BarcodeRevealTool - Architecture & SOLID Refactoring Proposal

## Table of Contents
1. [Current Architecture](#current-architecture)
2. [SOLID Issues & Problems](#solid-issues--problems)
3. [Proposed Architecture](#proposed-architecture)
4. [Implementation Roadmap](#implementation-roadmap)

---

## Current Architecture

### Current Layer Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                    PRESENTATION LAYER                       │
│           (SpectreConsoleOutputProvider)                     │
│              [UI Rendering & Display]                        │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                 APPLICATION LAYER                            │
│                   (GameEngine)                                │
│          [Game State Monitoring & Orchestration]             │
└────────────────────────┬────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┐
        │                │                │
┌───────▼────────┐  ┌────▼────────┐  ┌───▼──────────┐
│ ReplayService  │  │GameLobby     │  │IOutputProvider
│ (God Object)   │  │Factory       │  │(Abstractions)
│                │  │              │  │
│ ❌ Too Many    │  │ (Parsers)    │  └───────────────┘
│    Concerns    │  │              │
└────────┬───────┘  └──────────────┘
         │
┌────────▼──────────────────────────────┐
│        DATA ACCESS LAYER              │
│       (ReplayDatabase)                │
│  [SQL Queries, Cache, DB Schema]      │
│                                       │
│  ❌ Mixed: Query Logic + Persistence  │
└───────────────────────────────────────┘
```

### Current Class Responsibilities (Mixed & Unclear)

| Class | Current Responsibilities | Issues |
|-------|--------------------------|--------|
| **GameEngine** | Game state monitoring, Orchestration, Lobby processing, Display coordination | ❌ Violates SRP: Too many reasons to change |
| **ReplayService** | Cache management, Replay querying, Match history, Build orders, Replay file syncing | ❌ Violates SRP: 5+ responsibilities |
| **ReplayDatabase** | SQL execution, Query building, Data mapping, Schema migrations, Cache operations | ❌ Violates SRP: Data access + business logic |
| **GameLobbyFactory** | Lobby parsing, Player extraction, Team assignment, User identification | ❌ Violates SRP: Too tightly coupled to strategies |
| **ConfigBasedUserStrategy** | Only concern: User identification ✅ Good! | ✅ Well-defined responsibility |
| **SpectreConsoleOutputProvider** | Only concern: Console rendering ✅ Good! | ✅ Well-defined responsibility |

---

## SOLID Issues & Problems

### 1. **Single Responsibility Principle (SRP) Violations**

#### Problem: ReplayService (7+ Concerns)
```csharp
public class ReplayService {
    // Concern 1: Cache initialization
    public async Task InitializeCacheAsync() { }
    
    // Concern 2: Replay syncing from disk
    public async Task SyncReplaysFromDiskAsync() { }
    
    // Concern 3: Replay database operations
    public async Task SaveReplayToDbAsync() { }
    
    // Concern 4: Opponent match history retrieval
    public List<...> GetOpponentMatchHistory() { }
    
    // Concern 5: Build order queries
    public List<...> GetOpponentLastBuildOrder() { }
    
    // Concern 6: Games by opponent ID
    public List<...> GetGamesByOpponentId() { }
    
    // Concern 7: Cache statistics
    public (int, int) GetCacheStats() { }
}
```

**Impact:** 
- Hard to test (multiple test scenarios)
- Changes in cache logic break match history queries
- Database schema changes affect syncing logic
- Difficult to reuse components

#### Problem: GameEngine (4+ Concerns)
```csharp
public class GameEngine {
    // Concern 1: Game state monitoring
    private async Task MonitorGameStateAsync() { }
    
    // Concern 2: Lobby processing & parsing
    private async Task ProcessLobbyAsync() { }
    
    // Concern 3: Replay persistence
    private async Task OnExitingGameAsync() { }
    
    // Concern 4: Display coordination
    private void DisplayCurrentState() { }
}
```

**Impact:**
- Difficult to test game state independently
- UI coupling makes testing harder
- Game logic mixed with IO operations
- Difficult to support multiple output providers

### 2. **Open/Closed Principle (OCP) Violations**

**Problem:** Adding new match history query types requires modifying ReplayService
```csharp
// Current: Must add new method to ReplayService
public List<...> GetOpponentMatchHistoryByMap() { }  // New method
public List<...> GetOpponentMatchHistoryByRace() { } // New method
public List<...> GetOpponentMatchHistoryByDate() { } // New method
```

**Solution:** Use Query/Specification pattern instead

### 3. **Liskov Substitution Principle (LSP) - OK**
- Current strategy pattern is good
- IUserIdentificationStrategy implementations are substitutable ✅

### 4. **Interface Segregation Principle (ISP) Violations**

**Problem:** IReplayService is too fat (too many methods)
```csharp
public interface IReplayService {
    Task InitializeCacheAsync();           // Cache concern
    Task SyncReplaysFromDiskAsync();       // File syncing concern
    Task SaveReplayToDbAsync();            // Persistence concern
    List<...> GetOpponentMatchHistory();   // Query concern
    List<...> GetOpponentLastBuildOrder(); // Query concern
    List<...> GetGamesByOpponentId();      // Query concern
    (int, int) GetCacheStats();            // Statistics concern
}
```

**Impact:**
- Clients must depend on methods they don't use
- Makes mocking in tests harder
- Unclear what each client should depend on

### 5. **Dependency Inversion Principle (DIP) - Partially OK**
- ✅ Good: Using interfaces (IReplayService, IOutputProvider)
- ❌ Bad: GameEngine depends on concrete ReplayDatabase indirectly
- ❌ Bad: Dependencies are scattered, not centralized

---

## Proposed Architecture

### New Layered Architecture (Clean & Domain-Driven)

```
┌──────────────────────────────────────────────────────────────────┐
│                     PRESENTATION LAYER                            │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  SpectreConsoleOutputProvider                              │  │
│  │  └─ Implements: IGameStateRenderer, IOpponentRenderer      │  │
│  │     (Segregated by concern)                                │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
                           ▲
                           │ depends on
                           │
┌──────────────────────────────────────────────────────────────────┐
│                 APPLICATION/ORCHESTRATION LAYER                   │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  GameOrchestrator (Main Coordinator)                       │  │
│  │  ├─ Orchestrates: Game monitoring + Lobby processing       │  │
│  │  └─ Delegates to domain services                           │  │
│  └────────────────────────────────────────────────────────────┘  │
│                           │                                        │
│        ┌──────────────────┼──────────────────┐                    │
│        │                  │                  │                    │
│        ▼                  ▼                  ▼                    │
│  ┌───────────────┐ ┌────────────┐  ┌──────────────────┐         │
│  │ Lobby Domain  │ │ Game State │  │ Replay Sync      │         │
│  │ Service       │ │ Monitor    │  │ Service          │         │
│  │               │ │            │  │                  │         │
│  │ (Lobby        │ │ (Game      │  │ (File-to-DB      │         │
│  │  parsing &    │ │  state     │  │  synchronization)│         │
│  │  validation)  │ │  watching) │  │                  │         │
│  └───────────────┘ └────────────┘  └──────────────────┘         │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
                           ▲
                           │ depends on
                           │
┌──────────────────────────────────────────────────────────────────┐
│                    DOMAIN/BUSINESS LAYER                          │
│  ┌──────────────────────┐      ┌──────────────────────────────┐  │
│  │ Match History Domain │      │ Opponent Profile Domain      │  │
│  │ Service              │      │ Service                      │  │
│  │                      │      │                              │  │
│  │ Concerns:            │      │ Concerns:                    │  │
│  │ • Query match        │      │ • Get opponent stats         │  │
│  │ • Filter/Sort        │      │ • Build order history        │  │
│  │ • Calculate stats    │      │ • Win/loss tracking          │  │
│  └──────────────────────┘      └──────────────────────────────┘  │
│                                                                   │
│  ┌──────────────────────┐      ┌──────────────────────────────┐  │
│  │ Build Order Domain   │      │ Replay Cache Domain          │  │
│  │ Service              │      │ Service                      │  │
│  │                      │      │                              │  │
│  │ Concerns:            │      │ Concerns:                    │  │
│  │ • Parse build orders │      │ • Cache validation           │  │
│  │ • Extract timeline   │      │ • Cache invalidation         │  │
│  │ • Get last builds    │      │ • Cache statistics           │  │
│  └──────────────────────┘      └──────────────────────────────┘  │
│                                                                   │
└──────────────────────────────────────────────────────────────────┘
                           ▲
                           │ depends on
                           │
┌──────────────────────────────────────────────────────────────────┐
│                  INFRASTRUCTURE/DATA LAYER                        │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  IReplayRepository (Query Interface)                       │  │
│  │  └─ Abstraction for database queries                       │  │
│  │                                                            │  │
│  │  IReplayPersistence (Write Interface)                      │  │
│  │  └─ Abstraction for database writes                        │  │
│  │                                                            │  │
│  │  ICacheManager (Cache Interface)                           │  │
│  │  └─ Abstraction for cache operations                       │  │
│  └────────────────────────────────────────────────────────────┘  │
│                           │                                       │
│                           ▼                                       │
│  ┌────────────────────────────────────────────────────────────┐  │
│  │  ReplayDatabase (Persistence Implementation)               │  │
│  │  ├─ Implements: IReplayRepository, IReplayPersistence      │  │
│  │  └─ SQL Execution Only                                     │  │
│  │                                                            │  │
│  │  CacheManager (Cache Implementation)                       │  │
│  │  ├─ Implements: ICacheManager                              │  │
│  │  └─ Cache validation & invalidation logic                  │  │
│  └────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
```

---

### New Interfaces (Segregated by Concern)

#### Data Layer Interfaces
```csharp
/// <summary>
/// Query interface - Read-only access to replay data
/// Responsibility: Retrieve replay information for analysis
/// </summary>
public interface IReplayRepository
{
    // Match history queries
    List<MatchHistory> GetOpponentMatchHistory(string yourTag, string opponentTag, int limit);
    List<MatchHistory> GetMatchesByMap(string opponentTag, string mapName);
    List<MatchHistory> GetMatchesByRace(string opponentTag, string yourRace);
    
    // Build order queries
    List<BuildOrderEntry> GetBuildOrderForReplay(long replayId);
    List<BuildOrderEntry> GetOpponentLastBuildOrder(string opponentTag);
}

/// <summary>
/// Persistence interface - Write/Update access to replay data
/// Responsibility: Store and update replay information
/// </summary>
public interface IReplayPersistence
{
    // Replay storage
    long SaveReplay(ReplayData replay);
    void UpdateReplay(long replayId, ReplayData replay);
    
    // Build order storage
    void SaveBuildOrder(long replayId, List<BuildOrderEntry> entries);
}

/// <summary>
/// Cache management interface
/// Responsibility: Manage replay cache lifecycle
/// </summary>
public interface ICacheManager
{
    Task InitializeCacheAsync();
    Task SyncFromDiskAsync(string replayFolder, bool recursive);
    CacheStatistics GetStatistics();
    bool IsCacheValid();
}
```

#### Domain Layer Interfaces
```csharp
/// <summary>
/// Match history domain service
/// Responsibility: Query and analyze opponent match history
/// </summary>
public interface IMatchHistoryService
{
    List<MatchResult> GetOpponentHistory(string yourTag, string opponentTag, int limit);
    MatchStatistics AnalyzeHistory(List<MatchResult> matches);
    WinRate CalculateWinRate(List<MatchResult> matches);
}

/// <summary>
/// Build order domain service
/// Responsibility: Retrieve and analyze build order patterns
/// </summary>
public interface IBuildOrderService
{
    List<BuildOrderEntry> GetLastBuild(string opponentTag);
    BuildOrderPattern AnalyzeBuildTrend(string opponentTag, int lastGames);
    bool IsBuildOrderCached(string opponentTag);
}

/// <summary>
/// Opponent profile domain service
/// Responsibility: Aggregate opponent information
/// </summary>
public interface IOpponentProfileService
{
    OpponentProfile GetOpponentProfile(string opponentTag);
    OpponentStatistics GetStatistics(string opponentTag);
}
```

#### Presentation Layer Interfaces (Segregated)
```csharp
/// <summary>
/// Game state rendering - Only for game state display
/// </summary>
public interface IGameStateRenderer
{
    void RenderAwaitingState();
    void RenderInGameState(ISoloGameLobby lobby);
    void RenderStateTransition(string fromState, string toState);
}

/// <summary>
/// Match history rendering - Only for match history display
/// </summary>
public interface IMatchHistoryRenderer
{
    void RenderMatchHistory(List<MatchResult> matches);
    void RenderOpponentStatistics(OpponentStatistics stats);
}

/// <summary>
/// Build order rendering - Only for build order display
/// </summary>
public interface IBuildOrderRenderer
{
    void RenderBuildOrder(List<BuildOrderEntry> entries);
    void RenderBuildTrend(BuildOrderPattern pattern);
}

/// <summary>
/// Error/Warning rendering
/// </summary>
public interface IErrorRenderer
{
    void RenderError(string message);
    void RenderWarning(string message);
    void RenderInfo(string message);
}
```

---

### New Domain Classes (Representing Business Concepts)

```csharp
/// <summary>
/// Represents a single match result between two players
/// Domain concept: Encapsulates match outcome, timing, and opponent info
/// </summary>
public record MatchResult(
    string OpponentTag,
    DateTime GameDate,
    string Map,
    string YourRace,
    string OpponentRace,
    bool YouWon,
    string ReplayFile,
    string ReplayPath
);

/// <summary>
/// Represents build order pattern trends
/// Domain concept: Analyzes how opponent builds over time
/// </summary>
public record BuildOrderPattern(
    string OpponentTag,
    List<BuildOrderEntry> LastBuildOrder,
    int TotalGamesSampled,
    DateTime LastUpdated,
    string MostFrequentBuild
);

/// <summary>
/// Represents opponent profile and statistics
/// Domain concept: Single view of opponent information
/// </summary>
public record OpponentProfile(
    string OpponentTag,
    int TotalGames,
    WinRate AgainstYou,
    PreferredRaces PreferredRaces,
    List<string> FavoriteMaps,
    BuildOrderPattern CurrentBuildPattern,
    DateTime LastMet
);

/// <summary>
/// Represents win rate calculation
/// Domain concept: Meaningful metric for opponent analysis
/// </summary>
public record WinRate(
    int Wins,
    int Losses,
    double Percentage,
    int TotalGames
) 
{
    public bool IsSignificant => TotalGames >= 5; // At least 5 games
    public string AsPercentage => $"{Percentage:F1}%";
}
```

---

### New Service Classes (Focused Responsibilities)

#### Domain Service Example
```csharp
/// <summary>
/// Domain Service: Match History Analysis
/// Responsibility: Query and analyze opponent match history
/// Depends On: IReplayRepository (injected)
/// Used By: GameOrchestrator, IMatchHistoryRenderer
/// </summary>
public class MatchHistoryService : IMatchHistoryService
{
    private readonly IReplayRepository _repository;
    
    public MatchHistoryService(IReplayRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }
    
    /// <summary>
    /// Get opponent match history
    /// What: Retrieves matches against specific opponent
    /// Where: From replay database
    /// Why: Display opponent match history to player
    /// </summary>
    public List<MatchResult> GetOpponentHistory(
        string yourTag, 
        string opponentTag, 
        int limit = 5)
    {
        ArgumentException.ThrowIfNullOrEmpty(yourTag);
        ArgumentException.ThrowIfNullOrEmpty(opponentTag);
        
        return _repository.GetOpponentMatchHistory(yourTag, opponentTag, limit);
    }
    
    /// <summary>
    /// Analyze match history statistics
    /// What: Calculates win rate and trends
    /// Where: From match history
    /// Why: Understand opponent strength
    /// </summary>
    public MatchStatistics AnalyzeHistory(List<MatchResult> matches)
    {
        if (matches.Count == 0)
        {
            return MatchStatistics.Empty();
        }
        
        var wins = matches.Count(m => m.YouWon);
        var losses = matches.Count - wins;
        var winRate = new WinRate(wins, losses, (wins / (double)matches.Count) * 100, matches.Count);
        
        return new MatchStatistics(
            TotalGames: matches.Count,
            WinRate: winRate,
            AverageGameDate: matches.Average(m => m.GameDate.Ticks),
            MostPlayedMap: matches.GroupBy(m => m.Map).MaxBy(g => g.Count()).Key
        );
    }
}
```

---

### How to Break Down ReplayService

**BEFORE (God Object):**
```csharp
public class ReplayService {
    // 7+ responsibilities mixed together
    public async Task InitializeCacheAsync() { }
    public async Task SyncReplaysFromDiskAsync() { }
    public List<...> GetOpponentMatchHistory() { }
    public List<...> GetOpponentLastBuildOrder() { }
    // ... etc
}
```

**AFTER (Separated Concerns):**
```csharp
// Infrastructure layer - purely persistence
public class ReplayDatabase : IReplayRepository, IReplayPersistence { }
public class CacheManager : ICacheManager { }

// Application layer - orchestration
public class ReplaySyncService {
    // Only concern: Sync replays from disk to database
    public async Task SyncFromDiskAsync() { }
}

// Domain layer - business logic
public class MatchHistoryService : IMatchHistoryService { }
public class BuildOrderService : IBuildOrderService { }
public class OpponentProfileService : IOpponentProfileService { }
```

---

### How to Break Down GameEngine

**BEFORE (Mixed Concerns):**
```csharp
public class GameEngine {
    // Multiple concerns:
    private async Task MonitorGameStateAsync() { }
    private async Task ProcessLobbyAsync() { }
    private void DisplayCurrentState() { }
}
```

**AFTER (Separated Concerns):**
```csharp
// Monitors game process and state changes
public interface IGameStateMonitor {
    Task MonitorAsync(CancellationToken ct);
    event EventHandler<GameStateChangedEventArgs> StateChanged;
}

// Processes lobby data
public interface ILobbyProcessor {
    Task<ISoloGameLobby> ProcessLobbyAsync(string lobbyFilePath);
}

// Orchestrates all game-related operations
public class GameOrchestrator {
    private readonly IGameStateMonitor _stateMonitor;
    private readonly ILobbyProcessor _lobbyProcessor;
    private readonly IGameStateRenderer _renderer;
    
    public async Task RunAsync() {
        _stateMonitor.StateChanged += OnStateChanged;
        await _stateMonitor.MonitorAsync(_cancellationToken);
    }
    
    private async void OnStateChanged(GameStateChangedEventArgs args) {
        // Delegate to appropriate handler
        if (args.NewState == GameState.InGame) {
            var lobby = await _lobbyProcessor.ProcessLobbyAsync(args.LobbyPath);
            await HandleGameStarted(lobby);
        }
    }
}
```

---

## Implementation Roadmap

### Phase 1: Create New Interfaces (Non-Breaking)
```
Week 1:
├─ Create IReplayRepository interface
├─ Create ICacheManager interface
├─ Create IMatchHistoryService interface
├─ Create segregated renderer interfaces (IGameStateRenderer, IMatchHistoryRenderer, etc.)
└─ ✅ No breaking changes - just new interfaces
```

### Phase 2: Implement New Services
```
Week 2:
├─ Create MatchHistoryService implementing IMatchHistoryService
├─ Create BuildOrderService implementing IBuildOrderService
├─ Create OpponentProfileService implementing IOpponentProfileService
├─ Implement ReplayDatabase as IReplayRepository
├─ Implement CacheManager as ICacheManager
└─ ✅ New implementations - old code still works
```

### Phase 3: Create Domain Models
```
Week 3:
├─ Create MatchResult record
├─ Create BuildOrderPattern record
├─ Create OpponentProfile record
├─ Create WinRate record
├─ Create other domain value objects
└─ ✅ Pure domain models - no dependencies
```

### Phase 4: Refactor GameEngine
```
Week 4:
├─ Extract GameStateMonitor from GameEngine
├─ Extract LobbyProcessor from GameLobbyFactory
├─ Create GameOrchestrator to coordinate
├─ Update GameEngine to use new services
└─ ✅ GameEngine becomes thin orchestrator
```

### Phase 5: Update Presentation Layer
```
Week 5:
├─ Split SpectreConsoleOutputProvider by concern
├─ Implement segregated renderer interfaces
├─ Update dependency injection
└─ ✅ Multiple renderers, each with single concern
```

### Phase 6: Deprecate Old Code
```
Week 6:
├─ Mark ReplayService as Obsolete (pointing to new services)
├─ Create migration guide for consumers
├─ Update all internal dependencies
└─ ✅ Gradual deprecation, no big bang
```

---

## Benefits of Refactoring

| Aspect | Before | After |
|--------|--------|-------|
| **Testability** | Hard - God objects | Easy - Small focused classes |
| **Reusability** | Low - Tight coupling | High - Independent services |
| **Maintainability** | Hard - Many concerns | Easy - Single concern per class |
| **Adding Features** | Breaks existing code | Add without breaking (OCP) |
| **Error Localization** | Changes cascade | Changes isolated |
| **Mocking in Tests** | Difficult | Simple |
| **Code Clarity** | Confusing | Clear intent |
| **SOLID Score** | 40% | 85%+ |

---

## Example: Adding New Feature (Query By Map)

### BEFORE (Current - Requires modifying ReplayService)
```csharp
// Must modify existing class
public class ReplayService {
    // ... existing methods ...
    
    // ADD NEW METHOD HERE - Violates OCP!
    public List<...> GetOpponentMatchHistoryByMap(string opponentTag, string map) 
    { 
        return _database.GetOpponentMatchHistoryByMap(opponentTag, map);
    }
}
```

### AFTER (Proposed - Extend without modifying)
```csharp
// New service, no modifications needed - Follows OCP!
public class MapSpecificMatchHistoryService : IMatchHistoryService {
    private readonly IReplayRepository _repository;
    
    public List<MatchResult> GetOpponentHistory(
        string yourTag, 
        string opponentTag, 
        int limit = 5)
    {
        // Filters by map internally
        return _repository.GetMatchesByMap(opponentTag, "MyMap");
    }
}

// Register in DI container - that's it!
services.AddScoped<IMatchHistoryService, MapSpecificMatchHistoryService>();
```

---

## Conclusion

This refactoring brings:
- ✅ **Single Responsibility** - Each class has one reason to change
- ✅ **Open/Closed** - Extend without modifying existing code
- ✅ **Liskov Substitution** - Implementations are truly substitutable
- ✅ **Interface Segregation** - Clients depend on specific interfaces
- ✅ **Dependency Inversion** - Depend on abstractions, not concretions
- ✅ **Clean Code** - Clear intent, easy to read, self-documenting
- ✅ **Domain-Driven** - Domain concepts become first-class citizens
