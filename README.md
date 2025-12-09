# BarcodeRevealTool

**Version:** 0.2-alpha  
**Status:** Active Development - Opponent Profile System

A real-time StarCraft II game analyzer that reveals opponent information during gameplay by analyzing lobby data, build orders, and live ladder statistics from SC2Pulse.

> **Why This Tool?**  
> The barcode problem ruins ladder experience: you play against the same players repeatedly without knowing their identity or strategies. This tool automatically identifies opponents and reveals their most-used build orders by reading temporary replay files created during ranked games. Now you'll know exactly who you're playing against and what strategies to expect—no more mystery opponents!

## Quick Start

### What the Tool Does
1. **Detects opponents** from StarCraft II lobby files
2. **Tracks build orders** from your local replay cache
3. **Shows live ladder stats** from SC2Pulse (MMR, league, race preferences)
4. **Analyzes head-to-head history** between you and opponents
5. **Displays everything** in a clean console interface during gameplay

### How It Works Technically
The tool monitors a temporary file that SC2 creates when you enter a ranked game:
```
C:\Users\{username}\AppData\Local\Temp\StarCraft II\TempWriteReplayP1\replay.server.battlelobby
```

From this file, it extracts battle tags using pattern matching in the binary data. This file exists **only during a game** and is deleted when you return to the lobby—so the tool captures opponent data at the perfect moment.

Example from replay.server.battlelobby (hex dump):
```
00006E60  4F 72 69 67 69 6E 61 74 6F 72 23 32 31 34 33  Originator#21343
00006EA0  4E 69 6E 61 57 69 6C 6C 69 61 6D 73 23 36 30 33  NinaWilliams#603
```

## Features

### v0.2 (Current)
- **Real-time opponent detection** from lobby files
- **Live player statistics** from SC2Pulse API:
  - Current ladder rank and MMR
  - Peak achievements
  - Race-specific game counts
  - Professional player identification and social links
- **Build order extraction** and pattern analysis from replays
- **Head-to-head match history** from local replay cache
- **Preferred race detection** using actual game statistics
- **Persistent SQLite replay database** with automatic sync
- **Works offline** — captures replays even when tool isn't running
- **State-based UI** (minimal screen updates during gameplay)

### Opponent Profile Display
Shows comprehensive opponent information including:
- **Current Ladder Status**: League (color-coded), MMR, total games
- **Head-to-Head Record**: Win/loss vs you with percentage
- **Preferred Playstyle**: Main/secondary/tertiary races based on game counts
- **Recent Activity**: Last played timestamp
- **Favorite Opening**: Most frequent build order from replays

## Setup

### Prerequisites
- **.NET 8.0** or later
- **StarCraft II** installed

### Install & Run

```bash
git clone https://github.com/ppotepa/BarcodeRevealTool.git
cd BarcodeRevealTool
dotnet build -c Release
cd src/console-app/bin/Release/net8.0
./BarcodeRevealTool.ConsoleApp.exe
```

Edit `src/console-app/appsettings.json` first:

```json
{
  "barcodeReveal": {
    "user": {
      "battleTag": "YourTag#12345"
    },
    "replays": {
      "folder": "C:\\Users\\...\\StarCraft II\\Accounts\\...\\Replays\\Multiplayer",
      "recursive": true
    }
  }
}
```

**Important:** `user.battleTag` must be filled with your exact Battle.net tag (e.g., `Player#1234`). The tool uses this to:
1. Identify your matches in replay database
2. Calculate head-to-head statistics vs opponents
3. Match you with opponent data from SC2Pulse

On first run: scans and caches all replays (~1 minute per 500 replays). Subsequent starts sync new replays only.

## Architecture

**Layered Design - Clean Separation of Concerns:**

### BarcodeRevealTool.Engine
Cleanly layered and UI-agnostic core engine:

- **Application/** – Orchestration services
  - `GameOrchestrator` – Coordinates lobby monitoring, replay sync, and profile building
  - `GameStateMonitor` – Watches game state and lobby files
  - `ReplaySyncService` – Keeps replay cache updated
  - `LobbyProcessor` – Reads and parses lobby files
  
- **Domain/** – Business logic and value objects
  - `MatchHistoryService` – Analyzes head-to-head records from replays
  - `BuildOrderService` – Extracts and analyzes build patterns
  - `OpponentProfileService` – Combines SC2Pulse stats with replay history (async)
  - `Sc2PulsePlayerStatsService` – Fetches live player statistics from SC2Pulse API
  - Value objects: `OpponentProfile`, `SC2PulseStats`, `WinRate`, `PreferredRaces`
  
- **Presentation/** – Renderer interfaces (UI-agnostic)
  - `IGameStateRenderer` – Game state and lobby display
  - `IMatchHistoryRenderer` – Match history and opponent profiles
  - `IBuildOrderRenderer` – Build order and pattern analysis
  - `IErrorRenderer` – Error and warning messages
  
- **Infrastructure/** – Data access adapters
  - `ReplayDataAccess` – Repository pattern over `ReplayDatabase`
  - Exposes both repository and persistence abstractions
  
- **Game/Lobbies/** – Game-specific logic
  - Lobby parsing and player extraction
  - Team and player strategy utilities
  
- **Replay/** – Low-level replay handling
  - `BuildOrderReader` – Parses SC2 replay files using S2Protocol
  - `ReplayDatabase` – SQLite storage and queries
  - `ReplayCacheService` – Automatic replay discovery and caching

### BarcodeRevealTool.ConsoleApp
Spectre.Console UI host:

- `SpectreConsoleOutputProvider` – Implements all renderer interfaces with rich console formatting
  - Color-coded league indicators
  - Formatted tables and panels
  - Relative time display ("Today", "Yesterday", etc.)
  
- `Program.cs` – Dependency injection setup and application lifetime
  - Configures all services
  - Wires GameOrchestrator with renderers
  - Handles graceful shutdown
  
- `Adapters/GameLobbyFactoryAdapter` – Adapts existing lobby factory for engine

### Sc2Pulse (API Client)
- `Sc2PulseClient` – HTTP client for SC2Pulse API
  - Character search and profile endpoints
  - **NEW:** Full stats endpoint with race-specific data
  - Links to external profiles (Twitch, Twitter, Liquipedia, etc.)

### Data Persistence
**Single SQLite Database** (`replays.db`):
- Players table: battle tag, toon ID, MMR from SC2Pulse
- ReplayFiles table: replay metadata and file references
- BuildOrders table: parsed build order steps with timestamps
- Maps table: map information
- Insert-only policy: replays never deleted once cached
- Thread-safe access with locking during parallel processing

## Data Flow

```
Lobby Detection
    ↓
Extract your tag & opponent tag
    ↓
[Async Parallel Operations]
    ├─ Fetch from ReplayDatabase (local history)
    └─ Fetch from SC2Pulse API (live stats)
    ↓
Combine results into OpponentProfile
    ├─ SC2Pulse: League, MMR, Peak, Race stats
    ├─ Local Cache: Match history, Win rate, Last played
    └─ Inferred: Preferred races from game counts
    ↓
Display Opponent Profile
    ├─ League (color-coded)
    ├─ Head-to-head record
    ├─ Preferred races
    ├─ Recent activity
    └─ Favorite opening
```

## SC2Pulse Integration

**SC2Pulse is the single source of truth for player statistics:**

1. **Character Lookup**
   - Search by nickname: `CharacterFindQuery`
   - Returns basic player info and character ID

2. **Full Stats Fetch** (NEW)
   - Endpoint: `GET /sc2/api/character/{id}/stats/full`
   - Returns complete ladder ranking with wins/losses by race
   - Extracts actual game counts per race
   - Calculates win rate distribution

3. **Professional Player Data**
   - Aligulac ID and pro team affiliation
   - Earnings and tournament history
   - External links (Twitch, Twitter, Liquipedia, Discord, etc.)

## Roadmap

**v0.2 ✅** (Current)
- ✅ Opponent profiles with live SC2Pulse stats
- ✅ Race-specific game counts
- ✅ Professional player identification
- ✅ Build order history and patterns
- ✅ Head-to-head match tracking

**v0.3** (Planned)
- Streamer overlay support via REST API
- Advanced build order pattern recognition
- Opponent strength rating based on statistics
- Win rate by matchup (PvZ, PvT, PvP, etc.)

**v1.0** (Roadmap)
- Native GUI application
- Real-time in-game overlay
- Advanced replay analysis

**v1.1+**
- Machine learning for prediction
- Tournament history tracking
- Performance metrics and analytics

## Tech Stack

- **.NET 8.0** – Modern async/await patterns
- **SQLite** – Local replay cache with System.Data.SQLite
- **S2Protocol.NET** – StarCraft II replay file parsing
- **SC2Pulse API** – Live player statistics and rankings
- **Spectre.Console** – Rich console UI with colors and formatting
- **Serilog** – Structured logging to file
- **SqlKata** – SQL query builder for type-safe queries

## Limitations

- 1v1 multiplayer only (not archon/team modes)
- Console UI (GUI coming in v1.0)
- Requires manual Battle.net tag configuration
- SC2Pulse API availability dependent (graceful degradation if unavailable)
- Map name from file metadata (may not reflect actual map in some cases)

## Configuration

### appsettings.json
```json
{
  "barcodeReveal": {
    "user": {
      "battleTag": "YourName#1234"
    },
    "replays": {
      "folder": "C:\\Users\\YourName\\Documents\\StarCraft II\\Accounts\\...\\Replays\\Multiplayer",
      "recursive": true
    }
  }
}
```

### Logging
- Output: `logs/` directory (created automatically)
- Format: Structured JSON with timestamps
- Levels: Debug, Information, Warning, Error

## Debugging & Troubleshooting

### SC2Pulse Data Not Appearing

If opponent profiles show "Unknown" for league, MMR, or races:

**Quick Check:**
```powershell
# 1. View latest logs
Get-Content src/console-app/bin/Debug/net8.0/logs/*.log | Select-String "Fetching SC2Pulse|opponent profile" | Select-Object -Last 5

# 2. Test SC2Pulse API connectivity
$url = "https://sc2pulse.herokuapp.com/sc2/api/character/search/find?query=Alfir"
Invoke-RestMethod -Uri $url | Select-Object -First 1
```

**Expected Log Sequence:**
```
[INF] Building opponent profile for Opponent#12345
[INF] Fetching SC2Pulse stats for Opponent#12345, searching for nickname: Opponent
[INF] Found character Opponent#12345 (ID: XXXXX) for Opponent#12345
[INF] Retrieved SC2Pulse full stats: DIAMOND 3500 MMR, 400 games
```

**Common Issues:**

| Symptom | Cause | Solution |
|---------|-------|----------|
| "No characters found" warning | Character doesn't exist on SC2Pulse | Verify player's actual battle tag on sc2pulse.herokuapp.com |
| No logs mention "Fetching SC2Pulse" | BuildProfileAsync not called | Check if game detection is working (look for "Creating Solo 1v1 lobby") |
| Timeout errors | Network issue or SC2Pulse down | Test API endpoint manually with PowerShell (see above) |
| "Invalid battle tag format" | BattleTag parsing failed | Ensure lobby parser extracts format "Name#12345" |

### Checking Opponent Profile Display

The opponent profile display should show:
- **League**: Color-coded badge (BRONZE, SILVER, GOLD, PLATINUM, DIAMOND, MASTER, GRANDMASTER)
- **MMR**: Current ladder rating
- **Head-to-Head**: Your record vs this opponent (e.g., "5W - 3L")
- **Preferred Races**: Primary/Secondary/Tertiary based on game count distribution
- **Build Pattern**: Most recent build order played

If display shows all "Unknown" values, check logs for "Fetching SC2Pulse"—if not present, the profile building wasn't called.

## Contributing

Contributions welcome! Areas of interest:
- Replay parsing improvements and bug fixes
- Database optimization and query performance
- Overlay integration and UI enhancements
- SC2Pulse integration expansion
- Testing and bug reports
- Documentation improvements

## License

[Specify your license here]

## Disclaimer

Educational proof of concept. StarCraft II is a Blizzard Entertainment product. SC2Pulse is an independent community project. This tool is not affiliated with or endorsed by Blizzard Entertainment.

---

**Status:** v0.2 Active Development | **Last Updated:** December 9, 2025
