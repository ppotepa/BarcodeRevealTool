using System;
using s2protocol.NET;
using s2protocol.NET.Models;
using Serilog;

namespace BarcodeRevealTool.Persistence.Replay
{
    /// <summary>
    /// Extracts metadata from StarCraft II replay files.
    /// Parses replay files to extract player information, map, race selections, and game details.
    /// </summary>
    public static class ReplayMetadataExtractor
    {
        private static readonly ILogger Logger = Log.ForContext(typeof(ReplayMetadataExtractor));
        private static readonly ThreadLocal<ReplayDecoder> DecoderPool = new(() => new ReplayDecoder());
        private static readonly ReplayDecoderOptions DecoderOptions = new()
        {
            Initdata = true,
            Details = true,
            Metadata = true,
            TrackerEvents = false,
            MessageEvents = false,
            GameEvents = false,
            AttributeEvents = false
        };

        /// <summary>
        /// Extract metadata from a replay file by parsing its contents.
        /// SC2 replay files are ZIP archives containing game data.
        /// </summary>
        public static async Task<ReplayMetadata?> ExtractMetadataAsync(string replayFilePath)
        {
            try
            {
                if (!File.Exists(replayFilePath))
                {
                    Logger.Warning("Replay file not found: {FilePath}", replayFilePath);
                    return null;
                }

                var fileInfo = new FileInfo(replayFilePath);
                var fileName = fileInfo.Name;
                var fallbackDate = fileInfo.LastWriteTimeUtc;

                var metadata = await ParseReplayFileAsync(replayFilePath, fileName, fallbackDate);

                if (metadata == null)
                {
                    Logger.Warning("Failed to parse replay file: {FileName}", fileName);
                    return null;
                }

                Logger.Debug("Extracted metadata for replay: {FileName} - P1: {P1} vs P2: {P2}",
                    fileName, metadata.YourPlayer, metadata.OpponentPlayer);

                return metadata;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to extract metadata from replay: {FilePath}", replayFilePath);
                return null;
            }
        }

        private static async Task<ReplayMetadata?> ParseReplayFileAsync(string replayFilePath, string fileName, DateTime fallbackDateUtc)
        {
            try
            {
                var decoder = DecoderPool.Value ?? new ReplayDecoder();
                var replay = await decoder.DecodeAsync(replayFilePath, DecoderOptions);

                if (replay?.Details == null)
                {
                    Logger.Warning("Replay has no details section: {FilePath}", replayFilePath);
                    return null;
                }

                return MapReplayToMetadata(replay, replayFilePath, fileName, fallbackDateUtc);
            }
            catch (DecodeException ex)
            {
                Logger.Warning("Skipping replay {FilePath}: {Message}", replayFilePath, ex.Message);
                return null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Unexpected error parsing replay: {FilePath}", replayFilePath);
                return null;
            }
        }

        private static ReplayMetadata MapReplayToMetadata(Sc2Replay replay, string filePath, string fileName, DateTime fallbackDateUtc)
        {
            var detailsPlayers = replay.Details?.Players?
                .Where(p => p != null && p.Observe == 0)
                .OrderBy(p => p.TeamId)
                .ThenBy(p => p.WorkingSetSlotId)
                .ToList() ?? new List<DetailsPlayer>();

            var yourPlayer = detailsPlayers.ElementAtOrDefault(0);
            var opponentPlayer = detailsPlayers.ElementAtOrDefault(1);

            var gameDate = ResolveGameDate(replay.Details, fallbackDateUtc);

            var metadata = new ReplayMetadata
            {
                ReplayFilePath = filePath,
                GameDate = gameDate,
                Map = DetermineMapName(replay, fileName),
                YourPlayer = FormatPlayerName(yourPlayer),
                OpponentPlayer = FormatPlayerName(opponentPlayer),
                YourRace = NormalizeRace(yourPlayer?.Race),
                OpponentRace = NormalizeRace(opponentPlayer?.Race),
                YourPlayerId = FormatToonHandle(yourPlayer?.Toon),
                OpponentPlayerId = FormatToonHandle(opponentPlayer?.Toon),
                SC2ClientVersion = DetermineClientVersion(replay)
            };

            // Log toon extraction for debugging
            if (!string.IsNullOrEmpty(metadata.YourPlayerId) || !string.IsNullOrEmpty(metadata.OpponentPlayerId))
            {
                Logger.Debug("Toon IDs extracted for {FileName}: P1={YourToon} P2={OpponentToon}",
                    fileName, metadata.YourPlayerId ?? "N/A", metadata.OpponentPlayerId ?? "N/A");
            }

            metadata.ReplayGuid = ReplayMetadata.ComputeDeterministicGuid(fileName, metadata.GameDate);
            return metadata;
        }

        private static string DetermineMapName(Sc2Replay replay, string fileName)
        {
            var details = replay.Details;
            if (!string.IsNullOrWhiteSpace(details?.Title))
            {
                return details!.Title.Trim();
            }

            if (!string.IsNullOrWhiteSpace(details?.MapFileName))
            {
                return Path.GetFileNameWithoutExtension(details.MapFileName) ?? details.MapFileName;
            }

            if (!string.IsNullOrWhiteSpace(replay.Metadata?.Title))
            {
                return replay.Metadata!.Title.Trim();
            }

            return Path.GetFileNameWithoutExtension(fileName) ?? fileName;
        }

        private static string DetermineClientVersion(Sc2Replay replay)
        {
            if (replay.Metadata?.GameVersion is Version version)
            {
                return version.ToString();
            }

            if (!string.IsNullOrWhiteSpace(replay.Metadata?.BaseBuild))
            {
                return replay.Metadata!.BaseBuild!;
            }

            if (!string.IsNullOrWhiteSpace(replay.Metadata?.DataVersion))
            {
                return replay.Metadata!.DataVersion!;
            }

            if (replay.Header?.Version is Version headerVersion)
            {
                return headerVersion.ToString();
            }

            if (replay.Header?.BaseBuild is int baseBuild && baseBuild > 0)
            {
                return baseBuild.ToString();
            }

            return "Unknown";
        }

        private static string FormatPlayerName(DetailsPlayer? player)
        {
            if (player == null)
            {
                return "Unknown";
            }

            var clan = string.IsNullOrWhiteSpace(player.ClanName) ? string.Empty : $"[{player.ClanName}] ";
            var name = string.IsNullOrWhiteSpace(player.Name) ? "Unknown" : player.Name.Trim();
            return (clan + name).Trim();
        }

        private static string NormalizeRace(string? race)
        {
            if (string.IsNullOrWhiteSpace(race))
            {
                return "Unknown";
            }

            return race.ToLowerInvariant() switch
            {
                "terran" or "terr" => "Terran",
                "protoss" or "prot" => "Protoss",
                "zerg" => "Zerg",
                "random" => "Random",
                _ => race.Trim()
            };
        }

        private static string? FormatToonHandle(Toon? toon)
        {
            if (toon == null)
            {
                return null;
            }

            if (toon.Region <= 0 || toon.Id <= 0)
            {
                return null;
            }

            var programRaw = CleanToonSegment(toon.ProgramId);
            var program = string.IsNullOrWhiteSpace(programRaw) ? "S2" : programRaw.ToUpperInvariant();
            var realm = toon.Realm > 0 ? toon.Realm : 1;
            var rawHandle = $"{toon.Region}-{program}-{realm}-{toon.Id}";
            var sanitized = CleanToonSegment(rawHandle);
            return string.IsNullOrWhiteSpace(sanitized) ? null : sanitized;
        }

        private static string CleanToonSegment(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            Span<char> buffer = stackalloc char[value.Length];
            int idx = 0;
            foreach (var ch in value)
            {
                if (!char.IsControl(ch))
                {
                    buffer[idx++] = ch;
                }
            }

            return new string(buffer[..idx]).Trim();
        }

        private static DateTime ResolveGameDate(Details? details, DateTime fallbackDateUtc)
        {
            if (details?.DateTimeUTC is DateTime dateTime && dateTime > DateTime.MinValue.AddYears(100))
            {
                return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
            }

            return fallbackDateUtc;
        }

        /// <summary>
        /// Extract metadata from multiple replay files in parallel.
        /// </summary>
        public static async Task<List<(string filePath, ReplayMetadata? metadata)>> ExtractMetadataFromFilesAsync(
            string[] replayFiles,
            int maxDegreeOfParallelism = 4,
            Action<int, int>? onProgress = null)
        {
            var results = new List<(string, ReplayMetadata?)>();
            var semaphore = new System.Threading.SemaphoreSlim(maxDegreeOfParallelism);
            var tasks = new List<Task>();
            int processedCount = 0;
            var lockObj = new object();

            foreach (var replayFile in replayFiles)
            {
                await semaphore.WaitAsync();

                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var metadata = await ExtractMetadataAsync(replayFile);
                        lock (results)
                        {
                            results.Add((replayFile, metadata));

                            // Report progress
                            processedCount++;
                            onProgress?.Invoke(processedCount, replayFiles.Length);
                        }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            await Task.WhenAll(tasks);
            return results;
        }
    }
}
