# BarcodeRevealTool - Complete Architecture Documentation

**Version:** 0.2-alpha  
**Status:** Early Proof of Concept with Queue Detection  
**Last Updated:** December 8, 2025 (Rev 2)

---

## Table of Contents

1. [Project Overview](#project-overview)
2. [Recent Changes (v0.2)](#recent-changes-v02)
3. [Architecture Overview](#architecture-overview)
4. [Core Components](#core-components)
5. [Data Models](#data-models)
6. [Database Schema](#database-schema)
7. [StarCraft II Integration](#starcraft-ii-integration)
8. [Replay System](#replay-system)
9. [Data Flow](#data-flow)
10. [Dependency Injection](#dependency-injection)
11. [Configuration](#configuration)
12. [File Structure](#file-structure)

---

## Recent Changes (v0.2)

This release adds major improvements to database schema, user account management, opponent lookup optimization, and real-time game queue detection.

### Database Schema Redesign
- **Renamed columns** for semantic clarity: `Player1/Player2` → `You/Opponent`, `Race1/Race2` → `YourRace/OpponentRace`, `Player1Id/Player2Id` → `YouId/OpponentId`
- **Improved clarity** - Column names now explicitly indicate perspective (your player vs opponent player)
- **Updated all queries** - All SELECT, INSERT, and UPDATE statements use new column names

### User Account Auto-Discovery
- **Automatic detection** - System now discovers all SC2 accounts from `.lnk` files in SC2 account folder structure
- **Nick name mapping** - Extracts player nick names and discriminators from `.lnk` file names (format: `NickName_RealmID@RegionID.lnk`)
- **UserAccounts table** - New database table stores all discovered accounts (ToonHandle, BattleTag, Region, Realm, BattleNetId, timestamps)
- **Startup population** - Checks for new accounts on every startup using `INSERT OR IGNORE` (no data loss)
- **Debug display** - Shows discovered accounts on startup with format: `2-S2-1-10734203 (Admin#404) (Europe, Realm 1, ID 10734203)`

### Opponent Lookup Optimization
- **Fixed lookup failures** - Database was storing normalized toon handles (`S2-2-1369255`) but code was searching with region prefix (`1-S2-2-1369255`)
- **Flexible search strategy** - Now extracts battle.net ID from toon handle and uses LIKE pattern matching (`%1369255%`)
- **Handles format variations** - Robust to different toon handle formats across data sources
- **Result** - Opponent game lookups now work reliably when opponent has been tracked

### Real-Time Game Queue Detection
- **Automatic detection** - New `QueueDetectionService` queries SC2 local API (`http://127.0.0.1:6119/game`) to detect game queue type
- **Queue types supported**:
  - `LOTV_1V1` (2 players)
  - `LOTV_2V2` (4 players)
  - `LOTV_3V3` (6 players)
  - `LOTV_4V4` (8 players)
- **Integration** - Runs in parallel with SC2Pulse data loading during lobby detection
- **Error handling** - Graceful timeout (5 seconds) and error handling; returns null if detection fails
- **No user configuration** - Completely automatic, requires no manual input

---

## Project Overview

BarcodeRevealTool is a real-time StarCraft II game analyzer that reveals opponent information during gameplay by analyzing lobby data and build orders. It's designed to be non-intrusive, working entirely offline by capturing and analyzing replay files.

### Key Features

- **Real-time opponent detection** - Instantly identifies opponents when you launch a game
- **Automatic game queue detection** - Detects game mode (1v1, 2v2, 3v3, 4v4) from SC2 local API
- **Build order extraction** - Analyzes replay files to extract detailed build order information
- **Offline operation** - Works without internet; caches replays locally via SQLite
- **Smart caching** - First run scans all replays (~1 minute per 500 replays), subsequent runs sync only new replays
- **SC2 Pulse integration** - Fetches opponent MMR, rank, and statistics in real-time
- **User account auto-discovery** - Automatically detects and tracks all SC2 accounts on the machine
- **State-based UI** - Minimal screen updates; only refreshes when state changes

---

## Architecture Overview

The project follows a **layered, UI-agnostic architecture** with clear separation of concerns:

```
┌─────────────────────────────────────────────────┐
│         Console Application Layer                │
│  (BarcodeRevealTool.ConsoleApp)                 │
│  - Spectre.Console UI                           │
│  - Program.cs (Dependency Injection)            │
│  - OutputProvider Implementation                 │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│         Engine Layer (Core Logic)                │
│  (BarcodeRevealTool.Engine)                     │
│  - GameEngine.cs (State Machine)                │
│  - GameStateManager.cs                          │
│  - Abstract Interfaces (IOutputProvider, etc.)  │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│      Service & Database Layer                    │
│  - ReplayService (Orchestrator)                 │
│  - ReplayCacheService                           │
│  - ReplayDatabase / ReplayQueryService          │
│  - BuildOrderReader                             │
│  - Sc2PulseClient (External API)                │
└─────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────┐
│      External Dependencies                       │
│  - s2protocol.NET (Replay Decoding)             │
│  - SqlKata (Query Building)                     │
│  - SC2 Pulse API (Player Statistics)            │
│  - StarCraft II Client Files                    │
└─────────────────────────────────────────────────┘
```

### Design Principles

1. **UI Agnostic** - The engine doesn't know about UI; it communicates via `IOutputProvider` interface
2. **Testable** - All major components have interfaces; easy to mock and test
3. **Extensible** - New UI implementations (GUI, web, etc.) can be added without touching engine logic
4. **Efficient** - State-based updates reduce unnecessary screen refreshes; lazy loading of data
5. **Offline-First** - All critical functionality works offline using local SQLite database

---

## Core Components

### 1. GameEngine (src/engine/GameEngine.cs)

**Responsibility:** Main state machine and orchestrator of all game monitoring logic.

```csharp
public class GameEngine
{
    public enum ToolState { Awaiting, InGame }
    
    // Properties
    public ToolState CurrentState { get; }
    public AppSettings Configuration { get; }
    public Sc2PulseClient PulseClient { get; }
    
    // Events
    public event EventHandler<ToolStateChangedEventArgs> StateChanged;
    public event EventHandler<PeriodicStateEventArgs> PeriodicStateUpdate;
    
    // Methods
    public async Task Run();
    public void Stop();
}
```

**State Flow:**
```
┌─────────┐                                  ┌────────┐
│ Awaiting │ ← (Game closes) ← InGame ──→ │ InGame │
└─────────┘                                  └────────┘
    ↓
  [Waiting for SC2.exe to launch]
  [Waiting for replay.server.battlelobby to appear]
    ↓
  [Game started - transition to InGame]
```

**Key Methods:**
- `Run()` - Initializes cache, starts monitoring threads
- `DisplayCurrentState()` - Renders current game/opponent info
- `MonitorGameStateAsync()` - Checks for lobby file every 1500ms
- `MonitorSc2ProcessAsync()` - Tracks if SC2.exe is running

**Events Fired:**
- `StateChanged` - When transitioning between Awaiting ↔ InGame
- `PeriodicStateUpdate` - Every 1500ms for animations/status updates

---

### 2. GameStateManager (src/engine/GameStateManager.cs)

**Responsibility:** Tracks whether StarCraft II process is running and lobby file exists.

```csharp
public class GameStateManager
{
    public enum GameProcessState
    {
        NotRunning,    // SC2.exe not found
        Running,       // SC2.exe running but no lobby
        InMatch        // SC2.exe running + lobby detected
    }
    
    public GameProcessState CurrentState { get; }
    public bool IsGameRunning { get; }
    
    public bool IsStarCraft2Running();
    public void UpdateGameProcessState(bool hasLobbyFile);
    
    public event EventHandler<GameProcessStateChangedEventArgs> GameProcessStateChanged;
}
```

**SC2 Process Detection:**
- Looks for `SC2_x64.exe` (modern 64-bit) or `SC2.exe` (legacy 32-bit)
- Uses `Process.GetProcessesByName()` for fast detection

---

### 3. ReplayService (src/engine/Services/ReplayService.cs)

**Responsibility:** Orchestrates replay caching, syncing, and querying. The primary service used by the engine.

```csharp
public class ReplayService : IReplayService
{
    public Action? OnCacheOperationComplete { get; set; }
    
    // Initialization
    public async Task InitializeCacheAsync();
    public async Task SyncReplaysFromDiskAsync();
    
    // Querying
    public List<...> GetOpponentMatchHistory(string yourName, string opponentName, int limit = 10);
    public List<...> GetGamesByOpponentId(string yourId, string opponentId, int limit = 100);
    public List<...> GetOpponentLastBuildOrder(string opponentPlayerId, int limit = 20);
    
    // Single replay
    public async Task SaveReplayToDbAsync(string replayFilePath);
}
```

**Cache Lifecycle:**

1. **First Run (InitializeCacheAsync):**
   - Scans all replay files in configured folder (optionally recursive)
   - Decodes each replay using s2protocol.NET
   - Extracts: players, map, date, races, winner info
   - Stores in SQLite database
   - Takes ~1 minute per 500 replays

2. **Subsequent Runs (SyncReplaysFromDiskAsync):**
   - Gets all replay files from disk
   - Finds missing files (not in database)
   - Only decodes and stores new replays
   - Much faster than full scan

3. **On Game Exit (SaveReplayToDbAsync):**
   - Called when you exit a game
   - Saves just-played replay to database
   - File located at: `C:\Users\<user>\AppData\Local\Temp\Starcraft II\TempWriteReplayP1\LastReplay\`

---

### 4. BuildOrderReader (src/engine/Replay/BuildOrderReader.cs)

**Responsibility:** Decodes replay files and extracts metadata and build order information.

```csharp
public static class BuildOrderReader
{
    // Initialization
    public static void InitializeCache(string? customCachePath = null);
    public static ReplayDatabase? GetDatabase();
    
    // Decoding
    public static ReplayMetadata? GetReplayMetadataFast(string replayFilePath);
    public static async Task<BuildOrder> ExtractBuildOrderAsync(string replayFilePath);
    
    // Utilities
    public static string NormalizeToonHandle(string toonHandle);
    public static string? ExtractToonHandleLastBit(string toonHandle);
    public static PlayerInfo? FindPlayerInMetadata(ReplayMetadata metadata, string identifier);
}
```

**Key Process - GetReplayMetadataFast():**

1. Creates `ReplayDecoder` from s2protocol.NET
2. Sets decode options:
   - ✓ Metadata (title, version)
   - ✓ Details (player names, races, result)
   - ✗ TrackerEvents (build order timing - handled separately)
   - ✗ GameEvents, MessageEvents, AttributeEvents (not needed)
3. Decodes with 15-second timeout
4. Extracts from `replay.Details.Players`:
   - Name and BattleTag
   - Race (Protoss, Terran, Zerg)
   - Player ID (toon handle)
   - **Winner** (Player with Result == 1)
5. Returns `ReplayMetadata` object

**Toon Handle Normalization:**
- Format: `region-S2-realm-id` (e.g., `2-S2-1-1369255`)
- First digit is region code (1=Americas, 2=Europe, 3=Asia)
- Normalizing strips region: `2-S2-1-1369255` → `S2-1-1369255`
- Last bit extraction: `S2-1-1369255` → `1-1369255`

---

### 5. ReplayDatabase / ReplayQueryService

**ReplayDatabase (src/engine/Replay/ReplayDatabase.cs):**
- Direct SQLite access using raw ADO.NET
- Handles database schema initialization
- File-hash-based duplicate detection

**ReplayQueryService (src/engine/Replay/ReplayQueryService.cs):**
- SqlKata-based query building
- Type-safe queries with no SQL injection risk
- Used by ReplayService as primary data access layer

```csharp
public interface IReplayQueryService
{
    // Insert/Update
    long AddOrUpdateReplay(string player1, string player2, string map, 
                         string race1, string race2, DateTime gameDate, 
                         string replayFilePath, string? sc2ClientVersion = null,
                         string? player1Id = null, string? player2Id = null,
                         string? winner = null);
    
    // Queries
    List<ReplayRecord> GetReplaysWithPlayer(string playerName);
    List<...> GetOpponentMatchHistory(string yourName, string opponentName, int limit = 10);
    Queue<BuildOrderEntry> GetBuildOrderEntries(long replayId);
    
    // Navigation
    long? GetMostRecentOpponentReplayId(string yourName, string opponentName);
    long? GetNextOpponentReplayId(string yourName, string opponentName, DateTime currentDate);
    long? GetPreviousOpponentReplayId(string yourName, string opponentName, DateTime currentDate);
}
```

---

### 6. Sc2PulseClient (src/sc2pulse/Sc2PulseClient.cs)

**Responsibility:** Wraps SC2 Pulse public API for fetching player statistics.

**SC2 Pulse** is a community-maintained database of StarCraft II player statistics. API Base: `https://sc2pulse.nephest.com`

```csharp
public sealed class Sc2PulseClient : IDisposable
{
    // Character endpoints
    public async Task<List<LadderDistinctCharacter>?> FindCharactersAsync(
        CharacterFindQuery query, 
        CancellationToken cancellationToken = default);
    
    public async Task<List<LadderTeam>?> GetCharacterTeamsAsync(
        CharacterTeamsQuery? query = null, 
        CancellationToken cancellationToken = default);
    
    public async Task<CursorNavigableResultList<LadderMatch>?> GetCharacterMatchesAsync(
        CharacterMatchesQuery? query = null, 
        CancellationToken cancellationToken = default);
    
    public async Task<List<LadderDistinctCharacter>?> GetCharactersByIdAsync(
        List<long> ids, 
        CancellationToken cancellationToken = default);
}
```

**API Queries Used in BarcodeRevealTool:**

1. **FindCharactersAsync** - Search opponent by BattleTag
   - Returns: `LadderDistinctCharacter` with current MMR, rank, league, previous stats
   - Example: Find "Fantick#778" → Returns all characters with that name

2. **GetCharacterMatchesAsync** - Match history (Not currently used, but available)
   - Returns: Paginated list of matches with cursor navigation

---

### 7. GameLobbyFactory (src/engine/Game/Lobbies/GameLobbyFactory.cs)

**Responsibility:** Parses the StarCraft II battle lobby file into a game object.

**Lobby File Location:**
```
C:\Users\<username>\AppData\Local\Temp\Starcraft II\TempWriteReplayP1\replay.server.battlelobby
```

This binary file is created by SC2 when you launch a game. BarcodeRevealTool parses it to extract:
- Team compositions
- Player names and BattleTags
- Map name
- Game mode

```csharp
public interface IGameLobby
{
    Team? Team1 { get; }
    Team? Team2 { get; }
    string? Map { get; }
    // Additional game info...
}

public interface ISoloGameLobby : IGameLobby
{
    object? AdditionalData { get; set; }
    BuildOrderEntry? LastBuildOrderEntry { get; set; }
    Queue? DetectedQueue { get; set; }  // NEW: Auto-detected game queue type
    Task EnsureAdditionalDataLoadedAsync();
}
```

---

### 7b. QueueDetectionService (src/engine/Config/QueueDetectionService.cs)

**Responsibility:** Detects game queue type from StarCraft II local API endpoint.

**How It Works:**
1. Queries SC2's internal HTTP endpoint: `http://127.0.0.1:6119/game`
2. Parses JSON response to extract player count
3. Maps player count to Queue enum:
   - 2 players → `LOTV_1V1`
   - 4 players → `LOTV_2V2`
   - 6 players → `LOTV_3V3`
   - 8 players → `LOTV_4V4`
4. Returns queue type or null on failure/timeout

**Key Characteristics:**
- **No configuration required** - Automatically queries default endpoint
- **Async operation** - Non-blocking with 5-second timeout
- **Error resilient** - Gracefully handles timeouts, HTTP errors, JSON parsing failures
- **Parallel execution** - Runs alongside SC2Pulse data loading for efficiency

```csharp
public static class QueueDetectionService
{
    /// <summary>
    /// Detects game queue type by querying SC2 local API endpoint.
    /// </summary>
    /// <param name="timeoutSeconds">HTTP request timeout (default: 5 seconds)</param>
    /// <returns>Queue enum value (LOTV_1V1, LOTV_2V2, etc.) or null if detection fails</returns>
    public static async Task<Queue?> DetectQueueTypeAsync(int timeoutSeconds = 5);
    
    /// <summary>
    /// Gets total player count for a queue type.
    /// </summary>
    public static int GetPlayerCountFromQueue(Queue queue);
    
    /// <summary>
    /// Gets players per team for a queue type.
    /// </summary>
    public static int GetTeamSizeFromQueue(Queue queue);
}
```

**Integration with GameEngine:**
- Called in `ProcessLobbyAsync()` when game starts
- Runs in parallel with `EnsureAdditionalDataLoadedAsync()`
- Result stored in `_cachedLobby.DetectedQueue`
- Available for UI rendering and game analysis

---

### 8. IOutputProvider & SpectreConsoleOutputProvider

**Responsibility:** Abstraction for all UI rendering, allowing multiple implementations.

**IOutputProvider** (Interface):
```csharp
public interface IOutputProvider
{
    void Clear();
    void RenderAwaitingState();
    void RenderInGameState();
    void RenderLobbyInfo(ISoloGameLobby lobby, object? additionalData, 
                        object? lastBuildOrder, Player? opponentPlayer = null,
                        List<...>? opponentGames = null,
                        List<...>? opponentLastBuild = null);
    void RenderError(string message);
    void RenderWarning(string message);
    void HandlePeriodicStateUpdate(string state, ISoloGameLobby? lobby);
}
```

**SpectreConsoleOutputProvider** (Console Implementation):
```csharp
internal class SpectreConsoleOutputProvider : IOutputProvider
{
    // Uses Spectre.Console library for colorful tables, markup, etc.
    // Implements all IOutputProvider methods
    
    private void RenderLobbyInfo(...) { /* Tables with team info, opponent stats */ }
    private void RenderOpponentStats(LadderDistinctCharacter? stats) { /* MMR, rank, league */ }
    private void RenderOpponentGamesStats(...) { /* Head-to-head record */ }
    private void RenderLast5Matches(...) { /* Recent game results */ }
    private void RenderOpponentLastBuildOrder(...) { /* Build order table */ }
    private void RenderLastBuildOrder(...) { /* Your last build order */ }
}
```

---

### 8b. AccountToonDiscoveryService (src/engine/Config/AccountToonDiscoveryService.cs)

**Responsibility:** Auto-discovers SC2 accounts from file system without user configuration.

**Account Discovery Process:**
1. Scans `Documents\StarCraft II\Accounts\` folder structure
2. Parses `.lnk` (shortcut) files in account folders
3. Extracts toon handle, region, realm, and battle.net ID from folder names
4. Maps nick names to toon handles from `.lnk` file names (format: `NickName_RealmID@RegionID.lnk`)

**Data Extracted:**
- **Toon Handle**: Normalized format `S2-realm-battleNetId`
- **Region**: Numeric code (1=Americas, 2=Europe, 3=Asia)
- **Realm**: Blizzard realm ID
- **Battle.Net ID**: Unique player identifier
- **Nick Name**: Player's in-game nick name (from .lnk filename)

```csharp
public static class AccountToonDiscoveryService
{
    /// <summary>
    /// Discovers all toon handles from SC2 account folder structure.
    /// </summary>
    public static List<string> DiscoverAllToonHandles();
    
    /// <summary>
    /// Maps toon handles to (nick name, discriminator) pairs from .lnk files.
    /// </summary>
    public static Dictionary<string, (string NickName, string Discriminator)> DiscoverToonNickMapping();
    
    /// <summary>
    /// Extracts region from toon handle (first digit: 1, 2, or 3).
    /// </summary>
    public static int? ExtractRegion(string toonHandle);
    
    /// <summary>
    /// Extracts realm ID from toon handle (second-last digit).
    /// </summary>
    public static int? ExtractRealm(string toonHandle);
    
    /// <summary>
    /// Extracts battle.net ID from toon handle (last digits).
    /// </summary>
    public static int? ExtractBattleNetId(string toonHandle);
}
```

**Integration with GameEngine:**
- Called during GameEngine initialization via `UserDetectionService`
- Populates UserAccounts database table on first run
- Checks for new accounts on every startup using `INSERT OR IGNORE`
- Enables nick name display in debug info and UI

---

## Data Models

### ReplayMetadata

Lightweight metadata extracted from replay file:

```csharp
public class ReplayMetadata
{
    public string FilePath { get; set; }
    public Guid ReplayGuid { get; set; }  // Deterministic GUID based on filename + date
    public string Map { get; set; }
    public List<PlayerInfo> Players { get; set; }
    public DateTime GameDate { get; set; }
    public string? SC2ClientVersion { get; set; }
    public string? Winner { get; set; }  // Name of winning player
    public DateTime LastModified { get; internal set; }
}

public class PlayerInfo
{
    public string Name { get; set; }  // In-game name
    public string BattleTag { get; set; }  // BattleTag#discriminator
    public string Race { get; set; }  // Protoss, Terran, Zerg
    public string PlayerId { get; set; }  // Toon handle (S2-1-11050989)
}
```

### ReplayRecord

Database representation of a replay (using new v0.2 column names):

```csharp
public record ReplayRecord(
    long Id,
    string ReplayGuid,
    string You,                    // Player 1 (your perspective)
    string Opponent,               // Player 2 (opponent)
    string? YouId,                 // Your toon handle
    string? OpponentId,            // Opponent toon handle
    string Map,
    string YourRace,               // Your race (Protoss, Terran, Zerg)
    string OpponentRace,           // Opponent race
    DateTime GameDate,
    string ReplayFilePath,
    string FileHash,               // SHA256 for deduplication
    string? SC2ClientVersion,
    string? Winner,                // Name of winning player
    int BuildOrderCached,
    string? CachedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

**Column Name Changes (v0.2):**
- `Player1` → `You` (your perspective)
- `Player2` → `Opponent` (opponent)
- `Player1Id` → `YouId` (your toon handle)
- `Player2Id` → `OpponentId` (opponent toon handle)
- `Race1` → `YourRace` (your race)
- `Race2` → `OpponentRace` (opponent race)

### BuildOrderEntry

Single build order action:

```csharp
public record BuildOrderEntry(
    int PlayerId,
    double TimeSeconds,
    string Kind,  // "Building", "Unit", "Upgrade", "Tech"
    string Name   // "Hatchery", "Zergling", "Armor Upgrade", etc.
);
```

### SC2 Pulse Models

**LadderDistinctCharacter** - Opponent statistics:

```csharp
public class LadderDistinctCharacter
{
    public Members? Members { get; set; }  // Character info + stats
    public int RatingMax { get; set; }  // Peak MMR ever
    public string? LeagueMax { get; set; }  // Peak league (MASTER, DIAMOND, etc.)
    public Previousstats? PreviousStats { get; set; }  // Last season stats
    public Currentstats? CurrentStats { get; set; }  // This season stats
    public int TotalGamesPlayed { get; set; }  // Career games
}

public class Members
{
    public Character? Character { get; set; }  // Character details
    public int ProtossGamesPlayed { get; set; }
    public int TerranGamesPlayed { get; set; }
    public int ZergGamesPlayed { get; set; }
}

public class Character
{
    public int Id { get; set; }  // Character ID on SC2 Pulse
    public string? Name { get; set; }
    public string? Region { get; set; }  // 1 (Americas), 2 (Europe), 3 (Asia)
    public int Realm { get; set; }
    public int BattlenetId { get; set; }
    public string? Tag { get; set; }
    public string? BattleTag { get; set; }
}

public class Currentstats
{
    public int Rating { get; set; }  // Current MMR
    public int Rank { get; set; }  // Rank number (lower = better)
    public int GamesPlayed { get; set; }  // Games this season
}
```

### Game Models

**Player:**
```csharp
public record Player()
{
    public required string NickName { get; set; }  // In-game name
    public required string Tag { get; set; }  // BattleTag
}
```

**Team:**
```csharp
public record Team(string name)
{
    public HashSet<Player> Players { get; init; } = new();
}
```

**GameLobby:**
```csharp
public interface ISoloGameLobby
{
    Team? Team1 { get; }
    Team? Team2 { get; }
    string? Map { get; }
    object? AdditionalData { get; set; }  // SC2 Pulse opponent data
    BuildOrderEntry? LastBuildOrderEntry { get; set; }
}
```

---

## Database Schema

**Location:** `_db/replays.db` (SQLite)

**Replays Table (v0.2 - Updated Column Names):**
```sql
CREATE TABLE Replays (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReplayGuid TEXT NOT NULL UNIQUE,
    You TEXT NOT NULL,           -- Your player name (was Player1)
    Opponent TEXT NOT NULL,       -- Opponent player name (was Player2)
    YouId TEXT,                   -- Your toon handle (was Player1Id)
    OpponentId TEXT,              -- Opponent toon handle (was Player2Id)
    Map TEXT NOT NULL,
    YourRace TEXT NOT NULL,       -- Your race: Protoss, Terran, Zerg (was Race1)
    OpponentRace TEXT NOT NULL,   -- Opponent race (was Race2)
    GameDate TEXT NOT NULL,       -- ISO 8601 format
    ReplayFilePath TEXT NOT NULL UNIQUE,
    FileHash TEXT NOT NULL,       -- SHA256 for deduplication
    SC2ClientVersion TEXT,
    Winner TEXT,                  -- Name of winning player
    BuildOrderCached INTEGER DEFAULT 0,  -- 0 = not cached, 1 = build order extracted
    CachedAt TEXT,
    CreatedAt TEXT NOT NULL,
    UpdatedAt TEXT NOT NULL
);
```

**UserAccounts Table (NEW in v0.2):**
```sql
CREATE TABLE UserAccounts (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ToonHandle TEXT NOT NULL UNIQUE,  -- Normalized format: S2-realm-battleNetId
    BattleTag TEXT,                    -- BattleTag#discriminator
    Region INTEGER,                    -- 1=Americas, 2=Europe, 3=Asia
    Realm INTEGER,                     -- Blizzard realm ID
    BattleNetId INTEGER,               -- Unique player ID
    DiscoveredAt TEXT NOT NULL,        -- ISO 8601 timestamp
    UpdatedAt TEXT NOT NULL            -- ISO 8601 timestamp
);
```

**Purpose:** Stores all discovered SC2 accounts on the machine, auto-discovered at startup.

**BuildOrderEntries Table:**
```sql
CREATE TABLE BuildOrderEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ReplayId INTEGER NOT NULL,
    PlayerId INTEGER NOT NULL,  -- 1 or 2
    TimeSeconds REAL NOT NULL,
    Kind TEXT NOT NULL,  -- Building, Unit, Upgrade, Tech
    Name TEXT NOT NULL,  -- Structure/unit/upgrade name
    FOREIGN KEY (ReplayId) REFERENCES Replays(Id) ON DELETE CASCADE
);
```

**ConfigMetadata Table:**
```sql
CREATE TABLE ConfigMetadata (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    ReplayFolderPath TEXT,
    RecursiveScan INTEGER DEFAULT 1,
    ConfigHash TEXT NOT NULL,
    LastValidated TEXT NOT NULL
);
```

**Indexes** (for fast queries):
- `idx_Replays_ReplayGuid`
- `idx_Replays_You`, `idx_Replays_Opponent` (updated column names)
- `idx_Replays_GameDate`
- `idx_Replays_BuildOrderCached`
- `idx_BuildOrderEntries_ReplayId`
- `idx_UserAccounts_ToonHandle` (UNIQUE index)

**Views** (convenience queries):
- `ReplaysByPlayers` - Joins with build order entry count for easy lookups

**Design Principles:**
- **Insert-Only Policy** - Replays are never deleted or modified (append-only log)
- **Deduplication** - File hash + ReplayGuid prevent duplicates
- **Deterministic GUID** - Same replay file always gets same GUID (based on filename + date)
- **Cascading Delete** - If replay is deleted, build order entries cascade delete
- **Safe Account Updates** - UserAccounts uses `INSERT OR IGNORE` to avoid losing data when checking for new accounts

---

## StarCraft II Integration

### SC2 File Locations

The tool interacts with SC2 at specific filesystem locations:

**1. Lobby File** (most important for real-time detection):
```
C:\Users\<username>\AppData\Local\Temp\Starcraft II\TempWriteReplayP1\replay.server.battlelobby
```
- **When:** Created when you launch a game (any mode)
- **What:** Binary file containing game setup info (teams, map, players)
- **Used For:** Real-time opponent detection during lobby AND queue type detection
- **Lifecycle:** Exists while in lobby/game, deleted when you exit

**2. Replay Files:**
```
C:\Users\<username>\AppData\Local\StarCraft II\Accounts\<account-id>\Replays\Multiplayer\
```
- **When:** Created immediately after a game completes
- **What:** Binary replay file (SC2Replay format) containing full game replay
- **Used For:** Offline analysis (build orders, player stats, winner detection)
- **Note:** File appears in LastReplay folder first, then moves to date-based folders

**3. Account/Profile Links:**
```
C:\Users\<username>\AppData\Local\StarCraft II\Accounts\<account-id>\
```
- **File:** `BattleNet.xml` or similar
- **Contains:** Your linked BattleNet account info
- **Used For:** Auto-detection of your account (if not configured in appsettings.json)

**4. Data Directory (Tool's Cache):**
```
<tool-directory>\_db\
```
- **SQLite Database:** `replays.db` - All cached replay metadata and build orders
- **Size:** Grows slowly (~1KB per replay)

### SC2 Replay File Format

**s2protocol.NET Library:**
The tool uses the s2protocol.NET NuGet package to decode SC2 replay files (binary format).

**Replay Structure:**
```
SC2Replay File
├── Metadata (text) - Map name, game version
├── Details (binary) - Player names, races, IDs, result
├── TrackerEvents (binary) - Unit/structure events (for build order)
├── GameEvents (binary) - In-game events
├── MessageEvents (binary) - Chat messages
└── AttributeEvents (binary) - Player attribute changes
```

**Fast Decoding Strategy:**
The tool only decodes Metadata + Details (skips events) for first pass:
- Extracts: map name, player names, races, winner
- Uses 15-second timeout per replay to prevent hangs on corrupted files
- Fast enough for ~30-50 replays per second on modern hardware

### Blizzard Client Integration

While BarcodeRevealTool doesn't directly hook into Blizzard's client, it leverages:

1. **Lobby File** - Blizzard writes this when you queue/start a game
2. **Replay Files** - Blizzard creates these after each game
3. **Account Link** - Blizzard's local account configuration

The tool **never modifies** Blizzard files; it only reads them.

### SC2 Process Monitoring

The tool tracks the SC2 process by name:
- **SC2_x64.exe** - Modern 64-bit client (most common)
- **SC2.exe** - Legacy 32-bit client

Uses `System.Diagnostics.Process.GetProcessesByName()` for detection (Windows API).

### SC2 Local API Endpoint (NEW in v0.2)

StarCraft II exposes a local HTTP API on `http://127.0.0.1:6119/game` when the game is active:

**Endpoint:** `GET http://127.0.0.1:6119/game`
**Purpose:** Query live game state information
**Response Format:** JSON containing:
- Players array with player information
- GameState details
- Other game metadata

**Used For:**
- Queue type detection (1v1, 2v2, 3v3, 4v4)
- Real-time game state monitoring
- Automatic mode detection without user input

**Implementation Notes:**
- Only available when SC2 client is in-game
- No authentication required (local endpoint)
- Used by QueueDetectionService for automatic game mode detection

---

## Replay System

### How Replays Are Used

1. **During Game (Lobby Detection):**
   - Tool reads `replay.server.battlelobby` to get opponent name
   - Queries SC2 Pulse API for opponent stats in real-time
   - Queries SC2 local API to detect game queue type (1v1, 2v2, etc.)
   - Queries local database for past games vs this opponent
   - Displays opponent's build order from most recent matching game

2. **After Game (Replay Saving):**
   - Looks for replay file in `LastReplay` folder
   - Decodes it to extract: map, both players, races, winner
   - Stores metadata in database
   - Next time you play this opponent, their stats appear

3. **Build Order Analysis:**
   - Happens in background after caching phase
   - Extracts build events from TrackerEvents section
   - Stores: timestamp, unit/structure name, player ID
   - ~50KB per replay with build order cached

### Cache Initialization (First Run)

```
User launches tool
    ↓
ReplayService.InitializeCacheAsync()
    ↓
1. Get all SC2Replay files from configured folder
2. For each replay:
   a. Decode metadata (map, players, races, winner)
   b. Check if already in database (by GUID)
   c. If new: extract build order, store in database
3. Create indexes for fast queries
    ↓
UI displays: "Cache population complete. 1,234 replays indexed."
```

**Performance:** ~1 second per 20-30 replays (depends on disk/CPU)

### Cache Syncing (Subsequent Runs)

```
User launches tool
    ↓
ReplayService.SyncReplaysFromDiskAsync()
    ↓
1. Get all SC2Replay files from disk
2. Query database for already-cached files
3. Find missing files (on disk but not in DB)
4. Decode and store only the missing replays
    ↓
UI displays: "Synced 15 new replays from disk."
```

**Performance:** Very fast (only new files processed)

### Real-Time Replay Saving

When you exit a game:
```
GameEngine detects state change (InGame → Awaiting)
    ↓
GameEngine.OnExitingGameAsync()
    ↓
1. Wait 2 seconds (SC2 writes replay file)
2. Look for replay in C:\...\LastReplay\
3. Call ReplayService.SaveReplayToDbAsync()
4. Decode replay metadata
5. Store in database
    ↓
Next game vs same opponent will show updated stats
```

---

## Data Flow

### Scenario 1: Game Start (Opponent Detection)

```
User launches game in SC2
    ↓
GameStateManager.IsStarCraft2Running() → true
    ↓
GameEngine.MonitorGameStateAsync() checks for lobby file
    ↓
Lobby file appears: C:\...\replay.server.battlelobby
    ↓
GameStateManager updates state → InMatch
    ↓
GameEngine.StateChanged event fires
    ↓
GameLobbyFactory.ParseLobby(lobbyFilePath)
    ↓
Extract: Team1 players, Team2 players, Map
    ↓
Identify opponent from opposite team
    ↓
Launch parallel operations:
    ├─ SC2PulseClient.FindCharactersAsync(opponentName)
    │      ↓
    │      Returns: LadderDistinctCharacter (MMR, rank, stats)
    │
    └─ ReplayService.GetOpponentMatchHistory(yourName, opponentName, limit: 5)
         ↓
         ReplayQueryService queries local database
         ↓
         Returns: Last 5 games vs this opponent
    
Wait for both operations
    ↓
GameEngine calls OutputProvider.RenderLobbyInfo()
    ↓
UI displays:
    - Opponent name, toon handle, profile link
    - Current MMR, rank, peak league, career games
    - Your H2H record (Total games, last match date, recent maps)
    - Your last 5 games vs opponent (map, your race, their race)
    - Opponent's last build order (if cached)
```

### Scenario 2: Post-Game Save

```
Game completes
    ↓
GameStateManager.IsStarCraft2Running() → false
    ↓
GameEngine detects state change (InGame → Awaiting)
    ↓
GameEngine.OnExitingGameAsync()
    ↓
Wait 2 seconds (SC2 writes replay)
    ↓
Look for replay in C:\...\Starcraft II\TempWriteReplayP1\LastReplay\
    ↓
ReplayService.SaveReplayToDbAsync(replayFile)
    ↓
BuildOrderReader.GetReplayMetadataFast(replayFile)
    ↓
ReplayDecoder decodes replay (Metadata + Details only)
    ↓
Extract:
    - Map, player names, races, game date
    - Winner (player with Result == 1)
    - SC2 client version
    ↓
ReplayQueryService.AddOrUpdateReplay(...)
    ↓
Check if already in database by GUID
    ↓
If new: Insert replay record
    ↓
Next launch will have updated opponent stats
```

### Scenario 3: Opponent Build Order Lookup

```
Game starts, opponent identified
    ↓
ReplayService.GetOpponentLastBuildOrder(opponentId, limit: 20)
    ↓
ReplayQueryService.GetOpponentMatchHistory(yourId, opponentId)
    ↓
Find most recent game vs this opponent
    ↓
Get ReplayId from database
    ↓
ReplayQueryService.GetBuildOrderEntries(replayId)
    ↓
Query BuildOrderEntries table
    ↓
Filter by opponent's PlayerId (player 1 or 2)
    ↓
Order by TimeSeconds
    ↓
Return: List of (time, kind, name) tuples
    ↓
OutputProvider renders build order table with:
    - Time (MM:SS format)
    - Type (Building, Unit, Upgrade, Tech)
    - Name (Hatchery, Zergling, etc.)
```

---

## Dependency Injection

**Setup Location:** `Program.cs`

```csharp
var services = new ServiceCollection();

// Configuration
services.AddSingleton<IConfiguration>(config);

// Engine services (via extension method)
services.AddBarcodeRevealEngine();

// UI Provider
services.AddSingleton<IOutputProvider, SpectreConsoleOutputProvider>();

var serviceProvider = services.BuildServiceProvider();

// Get and run engine
var engine = serviceProvider.GetRequiredService<GameEngine>();
await engine.Run();
```

**ServiceExtensions.AddBarcodeRevealEngine():**

```csharp
public static void AddBarcodeRevealEngine(this IServiceCollection services)
{
    // Configuration
    services.AddSingleton(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var appSettings = new AppSettings();
        config.GetSection("barcodeReveal").Bind(appSettings);
        return appSettings;
    });

    // Core
    services.AddScoped<GameEngine>();
    services.AddScoped<GameLobbyFactory>();

    // Database & Queries
    services.AddScoped<IReplayQueryService, ReplayQueryService>();
    services.AddScoped<IBuildOrderCacheManager, BuildOrderCacheManager>();
    services.AddScoped<IReplayCacheService, ReplayCacheService>();

    // Services
    services.AddScoped<IReplayService, ReplayService>();
    services.AddScoped<Sc2PulseClient>();
}
```

**Lifetime Scopes:**
- **Singleton** - Configuration (shared across entire app)
- **Scoped** - Database services (new instance per request, shared within scope)
- **Transient** - (None currently; could be used for stateless utilities)

---

## Configuration

**Location:** `appsettings.json` (must be created manually)

```json
{
  "user": {
    "battleTag": "YourTag#12345"
  },
  "replays": {
    "folder": "C:\\Users\\<username>\\StarCraft II\\Accounts\\<account-id>\\Replays\\Multiplayer",
    "recursive": true,
    "showLastBuildOrder": false
  },
  "refreshInterval": 1500
}
```

**Configuration Classes:**

```csharp
public class AppSettings
{
    public UserSettings User { get; set; } = new();
    public ReplaysSettings Replays { get; set; } = new();
    public bool ExposeApi { get; set; }
}

public class UserSettings
{
    public string BattleTag { get; set; } = string.Empty;
}

public class ReplaysSettings
{
    public string Folder { get; set; } = string.Empty;  // Required
    public bool Recursive { get; set; }  // Scan subdirs?
    public bool ShowLastBuildOrder { get; set; } = false;  // TBD
}
```

**Auto-Detection:**
If `user.battleTag` is not set, the tool tries to auto-detect from SC2 account links:
- Looks in: `C:\Users\<username>\AppData\Local\StarCraft II\Accounts\`
- Parses account configuration files
- Sets Configuration.User.BattleTag automatically

---

## File Structure

```
BarcodeRevealTool/
├── src/
│   ├── console-app/                    # UI & Entry Point
│   │   ├── Program.cs                 # Dependency injection setup
│   │   ├── appsettings.json           # User configuration
│   │   ├── ui/
│   │   │   └── SpectreConsoleOutputProvider.cs  # Console UI implementation
│   │   ├── BarcodeRevealTool.ConsoleApp.csproj
│   │   └── bin/, obj/                 # Build artifacts
│   │
│   ├── engine/                         # Core Engine & Logic
│   │   ├── GameEngine.cs              # Main state machine
│   │   ├── GameStateManager.cs        # SC2 process monitoring
│   │   ├── Abstractions/              # Interfaces
│   │   │   ├── IOutputProvider.cs     # UI abstraction
│   │   │   ├── IReplayService.cs      # Replay management
│   │   │   └── IGameLobbyFactory.cs   # Lobby parsing
│   │   ├── Config/                    # Configuration
│   │   │   ├── AppSettings.cs
│   │   │   └── UserDetectionService.cs
│   │   ├── Extensions/
│   │   │   └── ServiceExtensions.cs   # DI setup
│   │   ├── Game/                      # Game models
│   │   │   ├── Player.cs
│   │   │   ├── Team.cs
│   │   │   ├── Profile.cs
│   │   │   └── Lobbies/
│   │   │       ├── IGameLobby.cs
│   │   │       ├── GameLobby.cs
│   │   │       └── GameLobbyFactory.cs
│   │   ├── Replay/                    # Replay system (core)
│   │   │   ├── ReplayDatabase.cs      # SQLite direct access
│   │   │   ├── ReplayQueryService.cs  # SqlKata queries
│   │   │   ├── ReplayCacheService.cs  # Cache management
│   │   │   ├── BuildOrderReader.cs    # s2protocol decoding
│   │   │   ├── ReplayMetadata.cs      # Metadata models
│   │   │   ├── Replay.cs              # Replay model
│   │   │   ├── BuildOrderEntry.cs     # Build order model
│   │   │   ├── BuildOrderCacheManager.cs
│   │   │   └── sql/
│   │   │       └── schema.sqlite      # Database schema
│   │   ├── Services/
│   │   │   └── ReplayService.cs       # Orchestrator service
│   │   ├── Models/
│   │   │   ├── OpponentMatchHistory.cs
│   │   │   └── BuildOrderEntry.cs
│   │   └── BarcodeRevealTool.Engine.csproj
│   │
│   └── sc2pulse/                       # SC2 Pulse API Client
│       ├── Sc2PulseClient.cs          # HTTP client
│       ├── Models/                    # API response models
│       │   ├── LadderDistinctCharacter.cs
│       │   ├── LadderTeam.cs
│       │   ├── LadderMatch.cs
│       │   ├── PlayerCharacter.cs
│       │   ├── Enums.cs
│       │   └── ... (20+ models)
│       ├── Queries/                   # Query builders
│       │   ├── CharacterFindQuery.cs
│       │   ├── CharacterTeamsQuery.cs
│       │   └── ... (10+ query classes)
│       └── Sc2PulseClient.csproj
│
├── BarcodeRevealTool.sln              # Solution file
├── README.md                           # Quick start guide
├── DOCUMENTATION.md                    # This file
└── .gitignore

_db/                                   # Created at runtime
└── replays.db                         # SQLite database (auto-created)
```

---

## Summary

**BarcodeRevealTool** is an elegantly architected tool that bridges StarCraft II with local replay analysis and real-time game detection:

### Core Features (v0.2)
1. **Game State Machine** - State machine monitoring SC2 process and lobby files for real-time opponent detection
2. **Queue Type Detection** - Automatic detection of game mode (1v1, 2v2, 3v3, 4v4) from SC2 local API
3. **Account Discovery** - Auto-discovery and tracking of all SC2 accounts on the machine
4. **Replay System** - Fast local database caching with s2protocol decoding and build order extraction
5. **API Integration** - SC2 Pulse client for real-time opponent statistics and SC2 local API for game state
6. **Opponent History** - Flexible LIKE pattern matching for reliable opponent lookup despite format variations
7. **UI Abstraction** - Clean interface allowing multiple UI implementations
8. **Offline-First** - All critical features work without internet

### Architectural Patterns
The system uses industry-standard patterns:
- ✓ Dependency Injection (Microsoft.Extensions.DependencyInjection)
- ✓ Repository Pattern (ReplayQueryService with SqlKata)
- ✓ Observer Pattern (Events for state changes)
- ✓ Factory Pattern (GameLobbyFactory)
- ✓ Strategy Pattern (IOutputProvider implementations)
- ✓ Service Layer Pattern (ReplayService orchestration)
- ✓ Configuration Pattern (AppSettings, auto-detection fallback)

### Database Design
- **Column naming** - Semantic perspective-aware names (You/Opponent/YourRace/OpponentRace)
- **Account tracking** - UserAccounts table with auto-discovery and `INSERT OR IGNORE` safety
- **Flexible lookups** - Battle.net ID extraction and LIKE pattern matching for robustness
- **Insert-only replays** - Append-only log for audit trail

### Recent Improvements (v0.2)
- ✓ Redesigned database schema with semantic column names
- ✓ Auto-discovery of SC2 accounts from file system
- ✓ Fixed opponent lookup with flexible LIKE pattern matching
- ✓ Real-time queue type detection from SC2 local API
- ✓ Parallel loading of SC2Pulse data and queue detection
- ✓ Safe account population with `INSERT OR IGNORE`

### Future Enhancements (v0.3+)
- GUI overlay using WPF or WinForms
- Build order history with win rates per queue type
- Queue-specific opponent statistics
- Advanced pattern recognition
- REST API for streamer overlays
- Machine learning on build order patterns
- Multi-game mode support (archon, co-op)

---

**Last Updated:** December 8, 2025  
**Version:** 0.2-alpha (with queue detection and account discovery)
