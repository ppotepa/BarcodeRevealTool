# BarcodeRevealTool

**Version:** 0.1-alpha  
**Status:** Early Proof of Concept

A real-time StarCraft II game analyzer that reveals opponent information during gameplay by analyzing lobby data and build orders.

## Overview

Monitors SC2 games and displays opponent info in real-time:

- Opponent battle tag identification
- Build order extraction from replays
- Persistent SQLite replay database
- Works offline - captures replays even when tool isn't running

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
- **StarCraft II** installed
- Replay files in your SC2 Accounts directory

### Installation

```bash
git clone https://github.com/ppotepa/BarcodeRevealTool.git
cd BarcodeRevealTool
dotnet build -c Release
```

### Configuration

Edit `src/tool/appsettings.json`:

```json
{
  "user": {
    "battleTag": "YourTag#12345"
  },
  "replays": {
    "folder": "C:\\Users\\...\\StarCraft II\\Accounts\\...\\Replays\\Multiplayer",
    "recursive": true
  },
  "refreshInterval": 1500
}
```

### Running

```bash
cd src/tool/bin/Release/net8.0
./BarcodeRevealTool.exe
```

Start a SC2 game - opponent info displays automatically.

### First Run

Scans and caches all replays (~1 minute per 500 replays). Creates `cache.lock` file. Subsequent startups sync new replays only.

## Architecture

**RevealTool.cs** - State machine, cache init, replay sync  
**GameLobbyFactory.cs** - Parse lobby file, enrich with SC2 Pulse API  
**BuildOrderReader.cs** - Extract build order from replays  
**ReplayDatabase.cs** - SQLite persistence, queries  

Database: Single `replays.db` with Players, Map, Date, SC2 Version, Build Orders. Insert-only policy.

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

## Tech Stack

- **.NET 8.0** framework
- **SQLite** database (System.Data.SQLite)
- **s2protocol.NET** - Replay file parsing
- **Sc2Pulse** - SC2 player API

## Building

```bash
dotnet build              # Debug
dotnet build -c Release   # Release
```

## Configuration

`appsettings.json` settings:

| Setting | Purpose |
|---------|---------|
| `user.battleTag` | Your SC2 battle tag |
| `replays.folder` | SC2 replay directory path |
| `replays.recursive` | Search subdirectories |
| `refreshInterval` | UI update interval (ms) |

## Data Storage

All data stored in `_db/` subfolder relative to executable:
- `replays.db` - Main replay database
- `cache.lock` - Lock file (prevents re-scanning on startup)

## Known Limitations (v0.1-alpha)

- 1v1 only
- Console UI (no GUI yet)
- Map name from file metadata only

## Performance

- **First startup:** ~1 minute per 500 replays
- **Subsequent startups:** <1 second (sync only)
- **In-game:** Minimal impact, state-based updates only
- **Database queries:** Indexed for fast lookups

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
