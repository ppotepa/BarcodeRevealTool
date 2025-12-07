# BarcodeRevealTool

**Version:** 0.1-alpha  
**Status:** Early Proof of Concept

A real-time StarCraft II game analyzer that reveals opponent information during gameplay by analyzing lobby data and build orders.

## Features

- Real-time opponent detection and build order extraction
- Persistent SQLite replay database with automatic sync
- Works offline—captures replays even when tool isn't running
- State-based UI (minimal screen updates during gameplay)

## Setup

### Prerequisites
- **.NET 8.0** or later
- **StarCraft II** installed

### Install & Run

```bash
git clone https://github.com/ppotepa/BarcodeRevealTool.git
cd BarcodeRevealTool
dotnet build -c Release
cd src/tool/bin/Release/net8.0
./BarcodeRevealTool.exe
```

Edit `src/tool/appsettings.json` first:

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

On first run: scans and caches all replays (~1 minute per 500 replays). Subsequent starts sync new replays only.

## Code

**Architecture:**
- **BarcodeRevealTool.Engine** - Core game engine logic, UI-agnostic
  - `GameEngine.cs` - Game state machine and replay sync
  - `Abstractions/` - IOutputProvider, IReplayService, IGameLobbyFactory interfaces
  
- **BarcodeRevealTool.ConsoleApp** - Minimal console application with Spectre.Console UI
  - `UI/SpectreConsoleOutputProvider.cs` - Colorful console output implementation
  - `Adapters/GameLobbyFactoryAdapter.cs` - Wraps GameLobbyFactory for engine
  - `Services/ReplayService.cs` - Replay cache/sync operations
  - `Program.cs` - Dependency injection setup

**Supporting Classes:**
- **GameLobbyFactory.cs** - Parse lobby file  
- **BuildOrderReader.cs** - Extract builds from replays  
- **ReplayDatabase.cs** - SQLite storage and queries

Database: Single `replays.db` with insert-only policy. Stores players, map, date, SC2 version, and build orders.

## Roadmap

**v0.2** - Build order history, win rate vs opponent, APM baselines  
**v0.3** - Local REST API for streamer overlays  
**v1.0** - GUI application with native overlay support  
**v1.1+** - Optional ML and advanced pattern recognition (only if it adds value)

## Tech Stack

- .NET 8.0
- SQLite (System.Data.SQLite)
- s2protocol.NET (replay parsing)
- Sc2Pulse API (player data)

## Limitations

- 1v1 only
- Console UI (no GUI yet)
- Map name from file metadata only

## Contributing

- Replay parsing improvements
- Database optimization
- UI/overlay integration
- Testing and bug reports

## License

[Specify your license here]

## Disclaimer

Educational proof of concept. StarCraft II is a Blizzard Entertainment product. Not affiliated with or endorsed by Blizzard.

---

**Status:** Early Development | **Last Updated:** December 7, 2025
