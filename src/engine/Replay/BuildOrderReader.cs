// Example: simple SC2 build-order extractor in .NET 8 using s2protocol.NET
// ------------------------------------------
// 1. Create a new project:
//      dotnet new console -n Sc2BuildOrder
//      cd Sc2BuildOrder
//      dotnet add package s2protocol.NET
// 2. Replace Program.cs with this file.
// 3. Run:
//      dotnet run "C:\path\to\your\replay.SC2Replay"
// ------------------------------------------

// Simple in-memory representation of a build order line
using BarcodeRevealTool.replay;
using s2protocol.NET;

namespace BarcodeRevealTool.Replay
{
    public static class BuildOrderReader
    {
        private const string ReplayExtension = "*.SC2Replay";
        private static ReplayDatabase? _database;

        /// <summary>
        /// Find a player in replay metadata by either battle tag or username.
        /// </summary>
        public static PlayerInfo? FindPlayerInMetadata(ReplayMetadata metadata, string playerIdentifier)
        {
            if (metadata?.Players == null)
                return null;

            // Try exact battle tag match first
            var playerByTag = metadata.Players.FirstOrDefault(p =>
                p.BattleTag.Equals(playerIdentifier, StringComparison.OrdinalIgnoreCase));

            if (playerByTag != null)
                return playerByTag;

            // Try case-insensitive name or partial match
            return metadata.Players.FirstOrDefault(p =>
                p.Name.Contains(playerIdentifier, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get cached build order for a replay file, or null if not yet cached.
        /// </summary>
        public static Queue<BuildOrderEntry>? GetCachedBuildOrder(string replayFilePath)
        {
            if (_database == null)
                return null;

            var replay = _database.GetReplayByFilePath(replayFilePath);
            if (replay?.BuildOrderCached == true)
            {
                return _database.GetBuildOrderEntries(replay.Id);
            }

            return null;
        }

        public static BuildOrderEntry? GetLastBuildOrderEntryByPlayerId(BuildOrder buildOrder, int playerId)
        {
            return buildOrder.Entries
                .Where(e => e.PlayerId == playerId)
                .OrderByDescending(e => e.TimeSeconds)
                .FirstOrDefault();
        }

        /// <summary>
        /// Fast method to extract replay metadata (players, game info) without full decoding.
        /// Uses cache to avoid re-processing known replays.
        /// </summary>
        public static ReplayMetadata? GetReplayMetadataFast(string replayFilePath)
        {
            try
            {
                var decoder = new ReplayDecoder();

                // Decode metadata, details, and tracker events for complete info
                var options = new ReplayDecoderOptions
                {
                    Details = true,
                    Metadata = true,
                    TrackerEvents = true,
                    GameEvents = false,
                    MessageEvents = false,
                    AttributeEvents = false
                };

                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15)); // 15 second timeout
                var replay = decoder.DecodeAsync(replayFilePath, options, cts.Token).GetAwaiter().GetResult();

                if (replay?.Details?.Players == null)
                    return null;

                // Extract basic game info
                string mapName = replay.Metadata?.Title ?? "Unknown Map";
                // Use file modification time as game date (reliable fallback)
                DateTime gameDate = new FileInfo(replayFilePath).LastWriteTime;
                string sc2Version = replay.Metadata?.GameVersion?.ToString() ?? "Unknown";

                var metadata = new ReplayMetadata
                {
                    FilePath = replayFilePath,
                    Map = mapName,
                    GameDate = gameDate,
                    SC2ClientVersion = sc2Version,
                    Players = replay.Details.Players.Select(p => new PlayerInfo
                    {
                        Name = p.Name ?? string.Empty,
                        BattleTag = ExtractBattleTag(p.Name),
                        Race = p.Race ?? "Unknown",
                        PlayerId = ExtractPlayerId(p)
                    }).ToList(),
                    LastModified = new FileInfo(replayFilePath).LastWriteTime
                };

                return metadata;
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"  ✗ Error reading {Path.GetFileName(replayFilePath)}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Initialize the cache and main database (call once at startup).
        /// </summary>
        public static void InitializeCache(string? customCachePath = null)
        {
            _database = new ReplayDatabase(customCachePath);
            var (total, withBuildOrder) = _database.GetDatabaseStats();
            // Console.WriteLine($"Database initialized: {total} replays ({withBuildOrder} with build order)");
        }

        /// <summary>
        /// Get the database instance for direct access (used by RevealTool for cache initialization).
        /// </summary>
        public static ReplayDatabase? GetDatabase()
        {
            return _database;
        }

        public static async Task<BuildOrder> Read(string replayFolderPath, string playerBattleTag, bool recursive, Func<game.lobbies.ISoloGameLobby, Game.Team> oppositeTeam)
        {
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var replayFiles = Directory.GetFiles(replayFolderPath, ReplayExtension, searchOption);

            if (replayFiles.Length == 0)
            {
                return new BuildOrder { Entries = new Queue<BuildOrderEntry>() };
            }

            // Find the most recent replay with the specified player using fast metadata extraction with caching
            var replayFile = FindMostRecentReplayWithPlayerFast(replayFiles, playerBattleTag);

            if (replayFile == null)
            {
                return new BuildOrder { Entries = new Queue<BuildOrderEntry>() };
            }

            var buildOrder = await ReadReplay(replayFile);

            // Store in database asynchronously in the background
            _ = StoreReplayInDatabaseAsync(replayFile, playerBattleTag, buildOrder);

            return buildOrder;
        }

        /// <summary>
        /// Extract battle tag from player name (format: "Name#12345").
        /// </summary>
        private static string ExtractBattleTag(string? playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                return string.Empty;

            var lastHashIndex = playerName.LastIndexOf('#');
            if (lastHashIndex > 0)
            {
                return playerName.Substring(lastHashIndex - 1);
            }

            return playerName;
        }

        /// <summary>
        /// <summary>
        /// Extract player toon handle from player object.
        /// Format: "region-S2-realm-id" (e.g., "2-S2-1-11057632")
        /// This is Blizzard's stable unique identifier for an account, 
        /// even if the player uses barcodes or changes their display name.
        /// </summary>
        private static string ExtractPlayerId(dynamic player)
        {
            try
            {
                // Try to access Toon object from s2protocol Player
                var toon = GetDynamicProperty(player, "Toon") 
                        ?? GetDynamicProperty(player, "m_toon");
                
                if (toon != null)
                {
                    int? region = GetDynamicPropertyAsInt(toon, "Region") 
                               ?? GetDynamicPropertyAsInt(toon, "m_region");
                    int? realm = GetDynamicPropertyAsInt(toon, "Realm") 
                              ?? GetDynamicPropertyAsInt(toon, "m_realm");
                    long? id = GetDynamicPropertyAsLong(toon, "Id") 
                            ?? GetDynamicPropertyAsLong(toon, "m_id");
                    
                    if (region.HasValue && realm.HasValue && id.HasValue)
                    {
                        return $"{region.Value}-S2-{realm.Value}-{id.Value}";
                    }
                }

                // Fallback: try UserId directly
                var userId = GetDynamicProperty(player, "UserId");
                if (userId != null)
                {
                    return userId.ToString() ?? string.Empty;
                }
            }
            catch
            {
                // Silently fail if property doesn't exist
            }

            return string.Empty;
        }

        /// <summary>
        /// Safely get a dynamic property from an object using reflection.
        /// </summary>
        private static object? GetDynamicProperty(dynamic obj, string propertyName)
        {
            try
            {
                var type = obj.GetType();
                var property = type.GetProperty(propertyName);
                return property?.GetValue(obj);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Safely get a dynamic property and convert to int.
        /// </summary>
        private static int? GetDynamicPropertyAsInt(dynamic obj, string propertyName)
        {
            try
            {
                var value = GetDynamicProperty(obj, propertyName);
                if (value == null) return null;
                
                if (value is int intVal) return intVal;
                if (int.TryParse(value.ToString(), out int result)) return result;
            }
            catch
            {
                // Silently fail
            }
            return null;
        }

        /// <summary>
        /// Safely get a dynamic property and convert to long.
        /// </summary>
        private static long? GetDynamicPropertyAsLong(dynamic obj, string propertyName)
        {
            try
            {
                var value = GetDynamicProperty(obj, propertyName);
                if (value == null) return null;
                
                if (value is long longVal) return longVal;
                if (long.TryParse(value.ToString(), out long result)) return result;
            }
            catch
            {
                // Silently fail
            }
            return null;
        }

        private static Queue<BuildOrderEntry> ExtractBuildOrder(Sc2Replay replay)
        {
            var result = new Queue<BuildOrderEntry>();

            // NOTE:
            // Property names below (Gameloop, UnitTypeName, ControlPlayerId, etc.)
            // follow the usual s2protocol naming. If anything doesn’t compile,
            // just inspect one event with // Console.WriteLine() / debugger and
            // tweak the property names accordingly.

            // 1) Units/buildings – SUnitInit/SUnitBorn (tracker events)
            if (replay.TrackerEvents?.SUnitBornEvents != null)
            {
                foreach (var e in replay.TrackerEvents.SUnitBornEvents)
                {
                    // e.ControlPlayerId / e.UnitTypeName / e.Gameloop are the usual names
                    int? controlPlayerId = e.ControlPlayerId;
                    if (controlPlayerId is null || controlPlayerId <= 0)
                        continue; // neutral or invalid

                    int playerId = controlPlayerId.Value;

                    int gameLoop = e.Gameloop;              // gameloop tick
                    double timeSeconds = gameLoop / 16.0;   // SC2 runs at 16 game loops per second
                    string unitName = e.UnitTypeName ?? "UnknownUnit";

                    // Filter out obvious junk (minerals, destructibles etc.) if needed
                    if (IsJunkUnit(unitName))
                        continue;

                    result.Append(new BuildOrderEntry(
                        PlayerId: playerId,
                        TimeSeconds: timeSeconds,
                        Kind: "Unit",
                        Name: unitName
                    ));
                }
            }

            // Optional: you can also include SUnitInitEvents to show *start* of buildings,
            // not only the moment they spawn. Uncomment if you want that:

            /*
            if (replay.TrackerEvents?.SUnitInitEvents != null)
            {
                foreach (var e in replay.TrackerEvents.SUnitInitEvents)
                {
                    int? controlPlayerId = e.ControlPlayerId;
                    if (controlPlayerId is null || controlPlayerId <= 0)
                        continue;

                    int playerId = controlPlayerId.Value;

                    int gameLoop = e.Gameloop;
                    double timeSeconds = gameLoop / 16.0;
                    string unitName = e.UnitTypeName ?? "UnknownUnit";

                    if (IsJunkUnit(unitName))
                        continue;

                    result.Add(new BuildOrderEntry(
                        PlayerId: playerId,
                        TimeSeconds: timeSeconds,
                        Kind: "Init",
                        Name: unitName
                    ));
                }
            }
            */

            // 2) Upgrades – SUpgradeEvents (tracker events)
            if (replay.TrackerEvents?.SUpgradeEvents != null)
            {
                foreach (var e in replay.TrackerEvents.SUpgradeEvents)
                {
                    int? player = e.PlayerId;
                    if (player is null || player <= 0)
                        continue;

                    int playerId = player.Value;
                    int gameLoop = e.Gameloop;
                    double timeSeconds = gameLoop / 16.0;

                    // upgrade name field is usually UpgradeTypeName
                    string upgradeName = e.UpgradeTypeName ?? "UnknownUpgrade";

                    result.Append(new BuildOrderEntry(
                        PlayerId: playerId,
                        TimeSeconds: timeSeconds,
                        Kind: "Upgrade",
                        Name: upgradeName
                    ));
                }
            }

            return result;
        }

        private static string? FindMostRecentReplayWithPlayerFast(string[] replayFiles, string playerBattleTag)
        {
            var validReplays = new List<(string FilePath, DateTime ModifiedTime)>();

            foreach (var replayFile in replayFiles)
            {
                var metadata = GetReplayMetadataFast(replayFile);

                if (metadata is not null && FindPlayerInMetadata(metadata, playerBattleTag) is not null)
                {
                    validReplays.Add((replayFile, metadata.LastModified));
                    // Console.WriteLine($"  ✓ {Path.GetFileName(replayFile)}");
                }
            }

            // Console.WriteLine($"Filtered to {validReplays.Count} replay(s) with player {playerBattleTag}.");

            // Return the most recent replay
            return validReplays
                .OrderByDescending(r => r.ModifiedTime)
                .FirstOrDefault()
                .FilePath;
        }

        // Very simple filter; tune this to your liking.
        private static bool IsJunkUnit(string unitTypeName)
        {
            if (string.IsNullOrEmpty(unitTypeName))
                return true;

            if (unitTypeName.StartsWith("MineralField", StringComparison.OrdinalIgnoreCase))
                return true;
            if (unitTypeName.StartsWith("VespeneGeyser", StringComparison.OrdinalIgnoreCase))
                return true;
            if (unitTypeName.StartsWith("Unbuildable", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static async Task<BuildOrder> ReadReplay(string replayFilePath)
        {
            var decoder = new ReplayDecoder();

            var options = new ReplayDecoderOptions
            {
                Details = true,
                Metadata = true,

                // We only need tracker events for a basic BO
                TrackerEvents = true,

                // Turn off stuff we don't need
                GameEvents = false,
                MessageEvents = false,
                AttributeEvents = false
            };

            using var cts = new CancellationTokenSource();

            Sc2Replay? replay = await decoder.DecodeAsync(replayFilePath, options, cts.Token);
            if (replay == null)
            {
                // Console.WriteLine("Failed to decode replay.");
                throw new InvalidOperationException("Failed to decode replay.");
            }

            var build = ExtractBuildOrder(replay);

            // Group and print per player
            foreach (var playerGroup in build
                         .GroupBy(e => e.PlayerId)
                         .OrderBy(g => g.Key))
            {
                var playerMeta = replay.Details?.Players
                    .FirstOrDefault(p =>
                    {
                        return p.TeamId == playerGroup.Key;
                    });

                string playerName = playerMeta?.Name ?? $"Player {playerGroup.Key}";
                string race = playerMeta?.Race ?? "Unknown";

                // Console.WriteLine($"=== Build order for {playerName} ({race}) ===");

                foreach (var entry in playerGroup.OrderBy(e => e.TimeSeconds))
                {
                    var t = TimeSpan.FromSeconds(entry.TimeSeconds);
                    // Console.WriteLine($"{t:mm\\:ss}  {entry.Kind,-8}  {entry.Name}");
                }

                // Console.WriteLine();
            }

            return new BuildOrder
            {
                Entries = build
            };
        }

        /// <summary>
        /// Store replay information and build order in database asynchronously.
        /// </summary>
        private static async Task StoreReplayInDatabaseAsync(string replayFilePath, string oppositePlayerTag, BuildOrder buildOrder)
        {
            try
            {
                await Task.Run(() =>
                {
                    if (_database == null)
                        return;

                    var metadata = GetReplayMetadataFast(replayFilePath);
                    if (metadata?.Players == null || metadata.Players.Count < 2)
                        return;

                    var player1 = metadata.Players[0];
                    var player2 = metadata.Players.Count > 1 ? metadata.Players[1] : player1;
                    var map = "Unknown";
                    var gameDate = metadata.LastModified;

                    // Store the replay record
                    var replayId = _database.AddOrUpdateReplay(
                        player1.Name,
                        player2.Name,
                        map,
                        player1.Race,
                        player2.Race,
                        gameDate,
                        replayFilePath
                    );

                    if (replayId > 0)
                    {
                        // Store build order entries
                        _database.StoreBuildOrderEntries(replayId, buildOrder.Entries);
                    }
                });
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"Error storing replay in database: {ex.Message}");
            }
        }
    }
}
