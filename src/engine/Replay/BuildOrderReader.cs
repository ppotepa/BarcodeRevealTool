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
using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Replay
{
    public static class BuildOrderReader
    {
        private static readonly Regex BattleTagRegex = new(@"(?i)^(?<name>[A-Za-z0-9]+)[#_](?<code>[0-9]{3,6})$", RegexOptions.Compiled);
        private const string ReplayExtension = "*.SC2Replay";
        private static ReplayDatabase? _database;

        /// <summary>
        /// Normalize a toonhandle by stripping the region prefix.
        /// Converts "2-S2-1-11050989" or "1-S2-1-11050989" to "S2-1-11050989"
        /// Also handles already-normalized handles.
        /// </summary>
        public static string NormalizeToonHandle(string toonHandle)
        {
            if (string.IsNullOrEmpty(toonHandle))
                return toonHandle;

            // Pattern: (X-)?S2-Y-Z where X is a single digit region (can be 1, 2, etc.)
            // We want to strip the leading "X-" if it exists
            var parts = toonHandle.Split('-');

            if (parts.Length >= 3)
            {
                // Check if first part is a single digit (region), and second is "S2"
                if (parts[0].Length == 1 && char.IsDigit(parts[0][0]) && parts[1] == "S2")
                {
                    // Has region prefix, strip it: skip first part and rejoin
                    return string.Join("-", parts.Skip(1));
                }
            }

            // Already normalized or doesn't match pattern, return as is
            return toonHandle;
        }

        /// <summary>
        /// Extract the last bit (player ID number) from a toonhandle.
        /// Example: "S2-1-11050989" → "11050989"
        /// </summary>
        public static string? ExtractToonHandleLastBit(string toonHandle)
        {
            if (string.IsNullOrEmpty(toonHandle))
                return null;

            var parts = toonHandle.Split('-');
            if (parts.Length >= 2)
            {
                // Return the last two parts: realm-id (e.g., "2-1369255" from "1-S2-2-1369255" or "S2-2-1369255")
                return $"{parts[^2]}-{parts[^1]}";
            }

            return null;
        }        /// <summary>
                 /// Find a player in replay metadata by either battle tag or username.
                 /// </summary>
        public static PlayerInfo? FindPlayerInMetadata(ReplayMetadata metadata, string playerIdentifier)
        {
            if (metadata?.Players == null)
                return null;

            // Normalize the search identifier to handle both formats
            string normalizedIdentifier = NormalizePlayerHandle(playerIdentifier);
            string normalizedToonHandle = NormalizeToonHandle(playerIdentifier);

            // Try exact battle tag match first
            var playerByTag = metadata.Players.FirstOrDefault(p =>
                NormalizePlayerHandle(p.BattleTag).Equals(normalizedIdentifier, StringComparison.OrdinalIgnoreCase));

            if (playerByTag != null)
                return playerByTag;

            // Try normalized toonhandle match
            var playerByNormalizedId = metadata.Players.FirstOrDefault(p =>
                NormalizeToonHandle(p.PlayerId).Equals(normalizedToonHandle, StringComparison.OrdinalIgnoreCase));

            if (playerByNormalizedId != null)
                return playerByNormalizedId;

            // Try partial match on the last bit of toonhandle (e.g., "11050989" from "S2-1-11050989")
            string? lastBit = ExtractToonHandleLastBit(playerIdentifier);
            if (!string.IsNullOrEmpty(lastBit))
            {
                var playerByPartialId = metadata.Players.FirstOrDefault(p =>
                    p.PlayerId.EndsWith(lastBit, StringComparison.OrdinalIgnoreCase));

                if (playerByPartialId != null)
                    return playerByPartialId;
            }

            // Try case-insensitive name or partial match
            return metadata.Players.FirstOrDefault(p =>
                NormalizePlayerHandle(p.Name).Contains(normalizedIdentifier, StringComparison.OrdinalIgnoreCase));
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

                // Decode only minimal required info for caching: metadata and details (no tracker events)
                // This is much faster since we don't need tracker events for basic cache info
                var options = new ReplayDecoderOptions
                {
                    Details = true,
                    Metadata = true,
                    TrackerEvents = false,  // Not needed for metadata extraction
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
                        Name = NormalizePlayerHandle(p.Name),
                        BattleTag = NormalizePlayerHandle(p.Name),
                        Race = p.Race ?? "Unknown",
                        PlayerId = ExtractPlayerId(p)
                    }).ToList(),
                    LastModified = new FileInfo(replayFilePath).LastWriteTime
                };

                return metadata;
            }
            catch (Exception)
            {
                // Silently ignore parsing errors, return null
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
            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] Read: Looking for replays with opponent {playerBattleTag}");

            // Try local database cache first
            var buildOrder = await TryGetBuildOrderFromCacheAsync(playerBattleTag);
            if (buildOrder != null)
            {
                return buildOrder;
            }

            // If not in cache, try external replay services
            // TODO: Implement alternative sources (e.g., query SC2Pulse replay service, other online replay databases)
            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] Read: Not found in cache, alternative sources not yet implemented");

            // Return empty if not found anywhere
            return new BuildOrder { Entries = new Queue<BuildOrderEntry>() };
        }

        /// <summary>
        /// Try to get build order from local cache database.
        /// Returns null if opponent not found in cache.
        /// </summary>
        private static async Task<BuildOrder?> TryGetBuildOrderFromCacheAsync(string playerBattleTag)
        {
            if (_database == null)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: Database not initialized");
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: Querying cache for {playerBattleTag}");
            var replaysWithOpponent = _database.GetReplaysWithPlayer(playerBattleTag);

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: Cache returned {replaysWithOpponent.Count} replays for {playerBattleTag}");

            if (replaysWithOpponent.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: No replays with opponent in cache");
                return null;
            }

            // Get the most recent replay (already sorted by date in DB)
            var mostRecentReplay = replaysWithOpponent
                .OrderByDescending(r => r.GameDate)
                .FirstOrDefault();

            if (mostRecentReplay == null)
            {
                return null;
            }

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: Found opponent replay in cache: {Path.GetFileName(mostRecentReplay.ReplayFilePath)}");

            // Check if build order is already cached in DB
            if (mostRecentReplay.BuildOrderCached)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: Build order already cached in DB");
                var buildOrderEntries = _database.GetBuildOrderEntries(mostRecentReplay.Id);
                return new BuildOrder
                {
                    Entries = buildOrderEntries,
                    Metadata = new ReplayMetadata
                    {
                        FilePath = mostRecentReplay.ReplayFilePath,
                        GameDate = mostRecentReplay.GameDate
                    }
                };
            }

            // Build order not cached yet - decode it now
            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] TryGetBuildOrderFromCacheAsync: Build order not cached, decoding replay");
            var buildOrder = await ReadReplay(mostRecentReplay.ReplayFilePath);

            // Store in database asynchronously in the background
            _ = StoreReplayInDatabaseAsync(mostRecentReplay.ReplayFilePath, playerBattleTag, buildOrder);

            return buildOrder;
        }


        /// <summary>
        /// Extract battle tag from player name (format: "Name#12345").
        /// </summary>
        private static string ExtractBattleTag(string? playerName) => NormalizePlayerHandle(playerName);

        private static string NormalizePlayerHandle(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sanitized = value.Trim().Replace('_', '#');
            var match = BattleTagRegex.Match(sanitized);
            if (match.Success)
            {
                return $"{match.Groups["name"].Value}#{match.Groups["code"].Value}";
            }

            return sanitized;
        }

        /// <summary>
        /// Extract player toon handle from player object.
        /// Format: "S2-realm-id" (e.g., "S2-1-11057632") - region prefix is stripped for normalization
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
                    int? realm = GetDynamicPropertyAsInt(toon, "Realm")
                              ?? GetDynamicPropertyAsInt(toon, "m_realm");
                    long? id = GetDynamicPropertyAsLong(toon, "Id")
                            ?? GetDynamicPropertyAsLong(toon, "m_id");

                    if (realm.HasValue && id.HasValue)
                    {
                        // Return normalized toonhandle without region prefix: "S2-realm-id"
                        return $"S2-{realm.Value}-{id.Value}";
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
            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Starting decode for {Path.GetFileName(replayFilePath)}");
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

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Decoding replay...");
            Sc2Replay? replay = await decoder.DecodeAsync(replayFilePath, options, cts.Token);
            if (replay == null)
            {
                // Console.WriteLine("Failed to decode replay.");
                throw new InvalidOperationException("Failed to decode replay.");
            }

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Decode complete, extracting build order...");
            var build = ExtractBuildOrder(replay);
            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Extracted {build.Count} build order entries");

            // Group and print per player
            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Starting grouping and display phase...");
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

                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: {playerName} ({race}) - {playerGroup.Count()} entries");

                foreach (var entry in playerGroup.OrderBy(e => e.TimeSeconds))
                {
                    var t = TimeSpan.FromSeconds(entry.TimeSeconds);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Complete, extracting replay metadata...");

            // Extract metadata from the replay object before clearing it
            string mapName = replay.Metadata?.Title ?? "Unknown Map";
            DateTime gameDate = new FileInfo(replayFilePath).LastWriteTime;
            string sc2Version = replay.Metadata?.GameVersion?.ToString() ?? "Unknown";

            // Extract winner from player results
            string? winner = null;
            if (replay.Details?.Players != null && replay.Details.Players.Count > 0)
            {
                // Find the player with the best result (usually 1 = won, 2 = lost)
                var playersByResult = replay.Details.Players
                    .OrderBy(p => p.Result)
                    .ToList();

                if (playersByResult.Count > 0)
                {
                    var winner_player = playersByResult.First();
                    winner = NormalizePlayerHandle(winner_player.Name) ?? "Unknown";
                    System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Winner identified as {winner} (result={winner_player.Result})");
                }
            }

            var replayMetadata = new ReplayMetadata
            {
                FilePath = replayFilePath,
                Map = mapName,
                GameDate = gameDate,
                SC2ClientVersion = sc2Version,
                Winner = winner,
                Players = replay.Details?.Players?.Select(p => new PlayerInfo
                {
                    Name = NormalizePlayerHandle(p.Name),
                    BattleTag = NormalizePlayerHandle(p.Name),
                    Race = p.Race ?? "Unknown",
                    PlayerId = ExtractPlayerId(p)
                }).ToList() ?? new List<PlayerInfo>(),
                LastModified = new FileInfo(replayFilePath).LastWriteTime
            };

            System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] ReadReplay: Complete, clearing replay object...");
            replay = null;

            return new BuildOrder
            {
                Entries = build,
                Metadata = replayMetadata
            };
        }

        /// <summary>
        /// Store replay information and build order in database asynchronously.
        /// </summary>
        private static async Task StoreReplayInDatabaseAsync(string replayFilePath, string oppositePlayerTag, BuildOrder buildOrder)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Starting background DB storage for {Path.GetFileName(replayFilePath)}");
                await Task.Run(() =>
                {
                    if (_database == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Database not initialized, skipping");
                        return;
                    }

                    // Use metadata already extracted during decode - no need to call GetReplayMetadataFast again!
                    if (buildOrder.Metadata is null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: No metadata in BuildOrder, skipping");
                        return;
                    }

                    var metadata = buildOrder.Metadata;
                    if (metadata.Players?.Count < 2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Metadata incomplete, skipping");
                        return;
                    }

                    var player1 = metadata.Players![0];
                    var player2 = metadata.Players!.Count > 1 ? metadata.Players![1] : player1;
                    var map = metadata.Map ?? "Unknown";
                    var gameDate = metadata.GameDate;

                    // Store the replay record
                    System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Adding/updating replay record...");
                    var replayId = _database.AddOrUpdateReplay(
                        player1.Name,
                        player2.Name,
                        map,
                        player1.Race,
                        player2.Race,
                        gameDate,
                        replayFilePath,
                        metadata.SC2ClientVersion,
                        player1.PlayerId,
                        player2.PlayerId,
                        metadata.Winner
                    );

                    if (replayId > 0)
                    {
                        // Store build order entries
                        System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Storing {buildOrder.Entries.Count} build order entries...");
                        _database.StoreBuildOrderEntries(replayId, buildOrder.Entries);
                        System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Complete");
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader] StoreReplayInDatabaseAsync: Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Extract the winner from a replay file by reading it with s2protocol.
        /// Returns the winner's normalized name, or null if unable to determine.
        /// </summary>
        public static string? ExtractWinnerFromReplayFile(string replayFilePath)
        {
            try
            {
                if (!File.Exists(replayFilePath))
                {
                    System.Diagnostics.Debug.WriteLine($"[BuildOrderReader.ExtractWinnerFromReplayFile] File not found: {replayFilePath}");
                    return null;
                }

                var decoder = new ReplayDecoder();
                var opts = new ReplayDecoderOptions
                {
                    Details = true,
                    Metadata = false,
                    TrackerEvents = false,
                    GameEvents = false,
                    MessageEvents = false,
                    AttributeEvents = false
                };

                var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10));
                var replay = decoder.DecodeAsync(replayFilePath, opts, cts.Token).GetAwaiter().GetResult();

                if (replay?.Details == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[BuildOrderReader.ExtractWinnerFromReplayFile] Details missing from replay: {replayFilePath}");
                    return null;
                }

                // Get players from details
                var playersEnum = GetProperty<System.Collections.IEnumerable>(replay.Details, "Players")
                                ?? GetProperty<System.Collections.IEnumerable>(replay.Details, "PlayerList");

                if (playersEnum == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[BuildOrderReader.ExtractWinnerFromReplayFile] No players found in Details");
                    return null;
                }

                var players = new List<(string name, int? result)>();
                foreach (var p in playersEnum)
                {
                    string name = GetProperty<string>(p, "Name")
                                ?? GetProperty<string>(p, "PlayerName")
                                ?? "<??>";

                    int? resultValue = GetProperty<int?>(p, "Result")
                                    ?? GetProperty<int?>(p, "m_result");

                    // Result: 1 = Victory, 2 = Defeat (typically)
                    players.Add((name, resultValue));
                }

                // Find winner (player with lowest result value, typically 1 for victory)
                if (players.Any())
                {
                    var winner = players.OrderBy(p => p.result ?? int.MaxValue).First();
                    var winnerName = NormalizePlayerHandle(winner.name);
                    System.Diagnostics.Debug.WriteLine($"[BuildOrderReader.ExtractWinnerFromReplayFile] Winner extracted: {winnerName} (result={winner.result})");
                    return winnerName;
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[BuildOrderReader.ExtractWinnerFromReplayFile] Error decoding replay: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Get a property from an object using reflection, supporting multiple property name patterns.
        /// </summary>
        private static T? GetProperty<T>(object obj, string propertyName)
        {
            if (obj == null)
                return default;

            var type = obj.GetType();
            var property = type.GetProperty(propertyName,
                System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (property != null)
            {
                var value = property.GetValue(obj);
                return (T?)Convert.ChangeType(value, typeof(T));
            }

            return default;
        }
    }
}
