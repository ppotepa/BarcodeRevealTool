using Sc2Pulse;
using Sc2Pulse.Models;
using Serilog;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    /// <summary>
    /// Manages season information fetched from SC2Pulse API.
    /// Caches seasons and determines the current active season for a given region.
    /// </summary>
    public class SeasonManager : IDisposable
    {
        private readonly Sc2PulseClient _client;
        private readonly ILogger _logger = Log.ForContext<SeasonManager>();
        private List<Season>? _cachedSeasons;
        private DateTime _cacheTime;
        private readonly TimeSpan _cacheExpiry = TimeSpan.FromHours(1);

        public SeasonManager()
        {
            _client = new Sc2PulseClient();
        }

        public void Dispose() => _client?.Dispose();

        /// <summary>
        /// Fetches current active season for a specific region.
        /// Returns the season where current UTC time falls within [Start, End).
        /// </summary>
        public async Task<Season?> GetCurrentSeasonAsync(string region = "EU", CancellationToken cancellationToken = default)
        {
            try
            {
                var seasons = await GetSeasonsAsync(cancellationToken);
                if (seasons == null || seasons.Count == 0)
                {
                    _logger.Warning("No seasons fetched from SC2Pulse API");
                    return null;
                }

                var now = DateTime.UtcNow;
                var currentSeason = seasons
                    .Where(s => s.Region == region && s.Start <= now && now < s.End)
                    .OrderByDescending(s => s.Number)
                    .FirstOrDefault();

                if (currentSeason != null)
                {
                    _logger.Information("Current season for {Region}: {Season}", region, currentSeason);
                }
                else
                {
                    _logger.Warning("No active season found for region {Region} at {Time}", region, now);
                }

                return currentSeason;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error fetching current season for region {Region}", region);
                return null;
            }
        }

        /// <summary>
        /// Fetches all seasons, using cache if available and not expired.
        /// </summary>
        private async Task<List<Season>?> GetSeasonsAsync(CancellationToken cancellationToken = default)
        {
            // Return cached if available and not expired
            if (_cachedSeasons != null && DateTime.UtcNow - _cacheTime < _cacheExpiry)
            {
                _logger.Debug("Using cached seasons (cached {CacheSeconds} seconds ago)",
                    (DateTime.UtcNow - _cacheTime).TotalSeconds);
                return _cachedSeasons;
            }

            _logger.Information("Fetching seasons from SC2Pulse API");
            var seasons = await _client.GetSeasonsAsync(cancellationToken);

            if (seasons != null)
            {
                _cachedSeasons = seasons;
                _cacheTime = DateTime.UtcNow;
                _logger.Information("Fetched {SeasonCount} seasons, caching for {CacheExpiryMinutes} minutes",
                    seasons.Count, _cacheExpiry.TotalMinutes);
            }
            else
            {
                _logger.Warning("SC2Pulse returned null seasons list");
            }

            return seasons;
        }
    }
}
