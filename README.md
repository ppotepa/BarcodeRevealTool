# BarcodeRevealTool

**Version:** 0.1-alpha  
**Status:** Early Proof of Concept

A real-time StarCraft II game analyzer that reveals opponent information during gameplay by analyzing lobby data and build orders.

## Overview

BarcodeRevealTool monitors your StarCraft II games and displays opponent information in real-time, including:

- **Battle Tag identification** - Identify your opponent by their battle tag
- **Build Order extraction** - View opponent's build order from replay data
- **Persistent replay database** - Maintains a SQLite database of all played replays
- **Offline-friendly** - Captures replays even when the tool isn't running

## Current Features (v0.1-alpha)

✅ Real-time game state detection  
✅ Opponent battle tag display  
✅ Build order extraction from replays  
✅ SQLite replay database with metadata caching  
✅ Automatic replay discovery and sync  
✅ Efficient state-based screen updates

## Getting Started

### Prerequisites

- **.NET 8.0** or later
- **StarCraft II** installed and playable
- **Replay files** in your StarCraft II Accounts directory

### Installation

1. Clone the repository:
```bash
git clone https://github.com/ppotepa/BarcodeRevealTool.git
cd BarcodeRevealTool
```

2. Build the project:
```bash
dotnet build -c Release
```

### Running the Tool

1. Configure `appsettings.json`:
```json
{
  "user": {
    "battleTag": "YourBattleTag#12345"
  },
  "replays": {
    "folder": "C:\\Users\\YourName\\Documents\\StarCraft II\\Accounts\\ACCOUNT_ID\\REALM-REGION\\Replays\\Multiplayer",
    "recursive": true
  },
  "refreshInterval": 1500,
  "exposeApi": false
}
```

2. Run the tool:
```bash
cd src/tool/bin/Release/net8.0
./BarcodeRevealTool.exe
```

3. Start a StarCraft II game - opponent info will display automatically

### First Run

On first startup, the tool will:
1. Scan your entire replay folder
2. Build a SQLite cache database (`_db/replays.db`)
3. Create a `cache.lock` file to prevent re-scanning
4. Display caching progress: `Caching in progress.... 1 of 512`

Subsequent startups sync any new replays recorded while the tool was offline.

## Architecture

### Core Components

**RevealTool.cs**
- Main application state machine (Awaiting / InGame)
- Handles cache initialization and replay synchronization
- Monitors game state changes with efficient polling

**GameLobbyFactory.cs**
- Parses lobby file data
- Enriches with SC2 Pulse API player stats
- Creates lobby display with team information

**BuildOrderReader.cs**
- Extracts build order from replay files using s2protocol.NET
- Manages metadata fast extraction
- Background async build order storage

**ReplayDatabase.cs**
- Single SQLite database for all replay persistence
- Stores: players, map, date, SC2 version, build orders
- Efficient queries with proper indexing
- Insert-only policy (never deletes)

### Database Schema

```
Replays (main table)
├── Id, ReplayGuid (deterministic: filename + date)
├── Player1, Player2, Map, Race1, Race2
├── GameDate, SC2ClientVersion
└── BuildOrderCached flag

BuildOrderEntries (linked table)
├── ReplayId (FK)
├── PlayerId, TimeSeconds
└── Kind, Name (unit/structure)
```

## Future Roadmap

### Phase 2 (v0.2-beta)
- [ ] **Build order history** - Search and filter opponent's last N builds by map/matchup
- [ ] **Player statistics** - Win rate vs this specific opponent
- [ ] **APM baselines** - Compare opponent's typical APM patterns across replays
- [ ] **Build confidence matching** - Estimate current opponent's likely build based on early game patterns

### Phase 3 (v0.3-release)
- [ ] **Local REST API** - Expose game data via HTTP for streamer overlays
- [ ] **Web dashboard** - View replay database and statistics
- [ ] **Advanced filtering** - Search by map, race matchup, time period, opponent skill level
- [ ] **Replay annotations** - Mark notable games and decisions

### Phase 4 (v1.0-stable) - Feature Complete
- [ ] **GUI application** - Dedicated windows UI replacing console
- [ ] **Streamer integration** - Native overlay support for OBS/Streamlabs
- [ ] **Multiple build order display** - Show last 5 opponent builds in-game
- [ ] **Real-time in-game stats** - Display opponent race, APM, recent matchup data while game is running
- [ ] **Build matching confidence scores** - Estimate opponent's build with % confidence based on early game
- [ ] **Tournament mode** - Anonymized statistics and VOD organization

### Phase 5 (v1.1+) - Optional Enhancements
- [ ] **Machine learning integration** (optional) - Predictive build classification if beneficial
- [ ] **Control group "barcode" analysis** - Advanced pattern recognition of opponent's unit control structure
- [ ] **Deep replay analysis** - Unit production patterns, spending efficiency, army composition timing
- [ ] **Community replay sharing** and collaborative analysis
- [ ] **Tournament statistics** and meta trends

> **Note:** v1.1+ features are aspirational. The tool may provide sufficient value without ML integration. These will be added only if they provide measurable improvements over pattern-based matching.

## Development

### Tech Stack
- **.NET 8.0** - Framework
- **System.Data.SQLite** - Database
- **s2protocol.NET** - Replay parsing
- **Sc2Pulse** - Player API integration
- **Microsoft.Extensions.Configuration** - Config management

### Building from Source
```bash
dotnet build              # Debug build
dotnet build -c Release   # Release build
dotnet test              # Run tests (when available)
```

### Project Structure
```
src/
├── sc2pulse/           # SC2 Pulse API client
├── tool/
│   ├── RevealTool.cs   # Main entry point
│   ├── config/         # Configuration models
│   ├── game/           # Game domain models
│   ├── replay/         # Replay processing & database
│   └── sql/            # Database schemas
```

## Configuration

Edit `appsettings.json`:

| Setting | Description | Default |
|---------|-------------|---------|
| `user.battleTag` | Your SC2 battle tag | "Originator#21343" |
| `replays.folder` | Path to SC2 replays | Windows default |
| `replays.recursive` | Scan subdirectories | false |
| `refreshInterval` | UI refresh interval (ms) | 1500 |
| `exposeApi` | Enable API server | false |

## Data Storage

All data stored in `_db/` subfolder relative to executable:
- `replays.db` - Main replay database
- `cache.lock` - Lock file (prevents re-scanning on startup)

## Known Limitations (v0.1-alpha)

- Only supports 1v1 games
- Battle tag from lobby (not full account name with region)
- Map name extracted from file metadata only
- No overlay integration yet
- No GUI - console-based only

## Performance

- **Startup (first run):** ~5-10 minutes for 500+ replays
- **Startup (cached):** <1 second
- **In-game updates:** Minimal CPU/memory impact
- **Database queries:** <100ms for player lookups

## Contributing

This is an early alpha project. Contributions welcome for:
- Replay parsing improvements
- Database optimization
- UI/overlay integration
- Testing and bug reports

## License

[Specify your license here]

## Disclaimer

This tool is a proof of concept for educational purposes. StarCraft II is a Blizzard Entertainment product. This tool is not affiliated with or endorsed by Blizzard Entertainment.

## Roadmap Rationale

**v0.1 (current):** Core functionality and database reliability  
**v0.2:** Build order search/history and player-specific statistics (APM, win rate vs opponent)  
**v0.3:** Streamer API and advanced filtering  
**v1.0:** Full GUI, native overlay support, in-game real-time stats  
**v1.1+:** Optional ML and pattern recognition (only if it adds value beyond deterministic matching)

The progression prioritizes practical, immediately useful features: knowing your opponent's build history and your historical matchup record is actionable from day one. Advanced features like ML will only be added if they demonstrably improve recommendations beyond simple pattern matching.

---

**Status:** Early Development | **Last Updated:** December 7, 2025
