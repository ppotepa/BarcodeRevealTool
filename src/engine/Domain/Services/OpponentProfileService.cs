using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Domain.Models;
using Serilog;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public class OpponentProfileService : IOpponentProfileService
    {
        private readonly IMatchHistoryService _matchHistoryService;
        private readonly IBuildOrderService _buildOrderService;
        private readonly ISc2PulsePlayerStatsService _pulseStatsService;
        private readonly AppSettings _settings;
        private readonly ILogger _logger = Log.ForContext<OpponentProfileService>();

        public OpponentProfileService(
            IMatchHistoryService matchHistoryService,
            IBuildOrderService buildOrderService,
            ISc2PulsePlayerStatsService pulseStatsService,
            AppSettings settings)
        {
            _matchHistoryService = matchHistoryService;
            _buildOrderService = buildOrderService;
            _pulseStatsService = pulseStatsService;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<OpponentProfile> BuildProfileAsync(string yourTag, string opponentTag, CancellationToken cancellationToken = default)
        {
            _logger.Information("Building opponent profile for {OpponentTag}", opponentTag);

            var matchLimit = Math.Max(1, _settings.Replays?.MatchHistoryLimit ?? 10);
            var opponentToon = _matchHistoryService.GetLastKnownOpponentToon(opponentTag);

            // Gather local replay cache data synchronously
            var history = _matchHistoryService.GetHistory(yourTag, opponentTag, matchLimit, opponentToon);
            var stats = _matchHistoryService.Analyze(history);
            var build = _buildOrderService.GetRecentBuild(opponentTag, 20);
            var pattern = _buildOrderService.AnalyzePattern(opponentTag, build);

            var lastPlayed = stats.LastGame ?? DateTime.MinValue;

            // Fetch live SC2Pulse data asynchronously - this is the source of truth
            SC2PulseStats? liveStats = null;
            IReadOnlyList<OpponentMatchSummary> recentMatches = Array.Empty<OpponentMatchSummary>();
            try
            {
                liveStats = await _pulseStatsService.GetPlayerStatsAsync(opponentTag, cancellationToken).ConfigureAwait(false);
                if (liveStats != null)
                {
                    _logger.Information("Retrieved SC2Pulse stats for {OpponentTag}: MMR {MMR} {League}",
                        opponentTag, liveStats.CurrentMMR, liveStats.CurrentLeague);

                    if (liveStats.CharacterId.HasValue)
                    {
                        recentMatches = await _pulseStatsService
                            .GetRecentMatchesAsync(liveStats.CharacterId.Value, matchLimit, cancellationToken)
                            .ConfigureAwait(false);
                    }
                }

                if (string.IsNullOrWhiteSpace(opponentToon) && !string.IsNullOrWhiteSpace(liveStats?.ToonHandle))
                {
                    opponentToon = liveStats.ToonHandle;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "Failed to fetch SC2Pulse stats for {OpponentTag}", opponentTag);
            }

            // Use SC2Pulse race data if available, otherwise fall back to replay history
            var preferredRaces = liveStats?.RaceStats != null
                ? DeterminePreferredRacesFromStats(liveStats.RaceStats)
                : new PreferredRaces(
                    history.GroupBy(m => m.OpponentRace)
                        .OrderByDescending(g => g.Count())
                        .Select(g => g.Key)
                        .FirstOrDefault() ?? "Unknown");

            // Empty list for favorite maps - SC2Pulse doesn't provide this, and we're removing replay-based maps
            var favoriteMaps = Array.Empty<string>();

            return new OpponentProfile(
                opponentTag,
                opponentToon,
                stats.WinRate,
                preferredRaces,
                favoriteMaps,
                pattern,
                lastPlayed,
                recentMatches,
                liveStats);
        }

        /// <summary>
        /// Determines preferred races based on race-specific win rates from SC2Pulse.
        /// Primary is the race with most games, secondary is second most, etc.
        /// </summary>
        private PreferredRaces DeterminePreferredRacesFromStats(WinRateByRace raceStats)
        {
            var races = new List<(string race, int games)>
            {
                ("Protoss", raceStats.Protoss.Wins + raceStats.Protoss.Losses),
                ("Terran", raceStats.Terran.Wins + raceStats.Terran.Losses),
                ("Zerg", raceStats.Zerg.Wins + raceStats.Zerg.Losses)
            };

            var sorted = races.OrderByDescending(r => r.games).ToList();

            return new PreferredRaces(
                Primary: sorted.FirstOrDefault(r => r.games > 0).race ?? "Unknown",
                Secondary: sorted.Skip(1).FirstOrDefault(r => r.games > 0).race,
                Tertiary: sorted.Skip(2).FirstOrDefault(r => r.games > 0).race);
        }
    }
}
