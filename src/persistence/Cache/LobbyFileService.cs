using Serilog;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;
using System.Security.Cryptography;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for managing lobby files and storing binary lobby file data.
    /// Uses the repository pattern (IUnitOfWork) for all database operations.
    /// Handles hashing, deduplication, and linking to runs.
    /// </summary>
    public class LobbyFileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger _logger = Log.ForContext<LobbyFileService>();

        public LobbyFileService(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <summary>
        /// Store a lobby file and return its database ID.
        /// Automatically handles SHA256 hashing and deduplication.
        /// </summary>
        public async Task<long> StoreLobbyFileAsync(
            int runNumber,
            string lobbyFilePath,
            int matchIndex,
            string? detectedOpponentTag = null,
            string? detectedOpponentToon = null,
            long? debugSessionId = null)
        {
            try
            {
                if (!File.Exists(lobbyFilePath))
                {
                    _logger.Error("Lobby file not found: {FilePath}", lobbyFilePath);
                    return 0;
                }

                // Read file and compute hash
                byte[] fileData = File.ReadAllBytes(lobbyFilePath);
                string fileHash = ComputeSHA256Hash(fileData);

                // Check for duplicates
                var existingId = await GetLobbyFileByHashAsync(fileHash);
                if (existingId.HasValue)
                {
                    _logger.Information("Lobby file already stored (hash match): {FilePath}", lobbyFilePath);
                    return existingId.Value;
                }

                // Create and store entity
                var lobbyFile = new LobbyFileEntity
                {
                    RunNumber = runNumber,
                    Sha256Hash = fileHash,
                    BinaryData = fileData,
                    MatchIndex = matchIndex,
                    DetectedPlayer1 = detectedOpponentTag,
                    DetectedPlayer2 = detectedOpponentToon
                };

                var newId = await _unitOfWork.LobbyFiles.AddAsync(lobbyFile);
                _logger.Information("Stored lobby file {FileName} (ID: {Id}, Size: {Size} bytes)",
                    Path.GetFileName(lobbyFilePath), newId, fileData.Length);

                return newId;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to store lobby file: {FilePath}", lobbyFilePath);
                return 0;
            }
        }

        /// <summary>
        /// Get lobby file by hash to detect duplicates.
        /// </summary>
        public async Task<long?> GetLobbyFileByHashAsync(string hash)
        {
            try
            {
                var allFiles = await _unitOfWork.LobbyFiles.GetAllAsync();
                var match = allFiles.FirstOrDefault(f =>
                    f.Sha256Hash?.Equals(hash, StringComparison.OrdinalIgnoreCase) ?? false
                );
                return match?.Id;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check for duplicate lobby file");
                return null;
            }
        }

        /// <summary>
        /// Get all lobby files for a specific run.
        /// </summary>
        public async Task<List<LobbyFileInfo>> GetLobbyFilesForRunAsync(int runNumber)
        {
            try
            {
                var files = await _unitOfWork.LobbyFiles.GetAllAsync(f => f.RunNumber == runNumber);

                return files
                    .OrderBy(f => f.MatchIndex)
                    .Select(f => new LobbyFileInfo
                    {
                        Id = f.Id,
                        MatchIndex = f.MatchIndex,
                        FileName = $"match_{f.MatchIndex}.sc2replay",
                        FileSize = f.BinaryData?.Length ?? 0,
                        OpponentTag = f.DetectedPlayer1,
                        OpponentToon = f.DetectedPlayer2,
                        StoredAt = f.CreatedAt
                    })
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to get lobby files for run {RunNumber}", runNumber);
                return new List<LobbyFileInfo>();
            }
        }

        public static string ComputeSHA256Hash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>
    /// Information about a stored lobby file.
    /// </summary>
    public class LobbyFileInfo
    {
        public long Id { get; set; }
        public int MatchIndex { get; set; }
        public string? FileName { get; set; }
        public int FileSize { get; set; }
        public string? OpponentTag { get; set; }
        public string? OpponentToon { get; set; }
        public DateTime StoredAt { get; set; }
    }
}
