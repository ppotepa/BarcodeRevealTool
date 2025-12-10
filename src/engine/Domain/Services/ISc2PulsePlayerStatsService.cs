using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    /// <summary>
    /// Provides access to live player statistics from SC2Pulse API.
    /// </summary>
    public interface ISc2PulsePlayerStatsService
    {
        /// <summary>
        /// Fetches live player statistics from SC2Pulse by battle tag.
        /// </summary>
        /// <param name="battleTag">Player's battle tag in format "Name#12345"</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Player statistics or null if not found</returns>
        Task<SC2PulseStats?> GetPlayerStatsAsync(string battleTag, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves recent SC2Pulse matches for the specified character id.
        /// </summary>
        Task<IReadOnlyList<OpponentMatchSummary>> GetRecentMatchesAsync(long characterId, int limit, CancellationToken cancellationToken = default);
    }
}
