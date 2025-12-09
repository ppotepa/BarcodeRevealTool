using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Domain.Models;
using Sc2Pulse;
using Sc2Pulse.Models;
using Sc2Pulse.Queries;
using Serilog;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    /// <summary>
    /// Fetches and enriches opponent data from SC2Pulse API.
    /// SC2Pulse is the single source of truth for all opponent statistics.
    /// </summary>
    public class Sc2PulsePlayerStatsService : ISc2PulsePlayerStatsService, IDisposable
    {
        private readonly Sc2PulseClient _client;
        private readonly SeasonManager _seasonManager;
        private readonly ILogger _logger = Log.ForContext<Sc2PulsePlayerStatsService>();

        public Sc2PulsePlayerStatsService()
        {
            _client = new Sc2PulseClient();
            _seasonManager = new SeasonManager();
        }

        /// <summary>
        /// Fetches live player statistics from SC2Pulse by battle tag.
        /// Uses current season data from character-teams endpoint for accurate seasonal stats.
        /// Returns null if player not found.
        /// </summary>
        public async Task<SC2PulseStats?> GetPlayerStatsAsync(string battleTag, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(battleTag))
                {
                    _logger.Debug("Battle tag is empty, skipping SC2Pulse lookup");
                    return null;
                }

                var parts = battleTag.Split('#');
                if (parts.Length != 2)
                {
                    _logger.Debug("Invalid battle tag format: {BattleTag}", battleTag);
                    return null;
                }

                var nickname = parts[0];
                _logger.Information("Fetching SC2Pulse stats for {BattleTag}, searching for nickname: {Nickname}", battleTag, nickname);

                // Get current season for EU region (can be made configurable)
                var currentSeason = await _seasonManager.GetCurrentSeasonAsync("EU", cancellationToken).ConfigureAwait(false);
                if (currentSeason == null)
                {
                    _logger.Warning("Could not determine current season, falling back to stats/full endpoint");
                    return await GetPlayerStatsLegacyAsync(battleTag, cancellationToken).ConfigureAwait(false);
                }

                _logger.Information("Current season for EU: {SeasonInfo}", currentSeason);

                // Search for the character by name
                var query = new CharacterFindQuery
                {
                    Query = nickname
                };

                var characters = await _client.FindCharactersAsync(query, cancellationToken).ConfigureAwait(false);
                if (characters == null || characters.Count == 0)
                {
                    _logger.Warning("No characters found for {Nickname}", nickname);
                    return null;
                }

                _logger.Debug("Found {CharacterCount} characters matching {Nickname}", characters.Count, nickname);

                // Find the exact match (prefer the one matching our battle tag)
                var character = characters.FirstOrDefault(c =>
                    c.Members?.Character?.Name?.Equals(battleTag, StringComparison.OrdinalIgnoreCase) ?? false);

                // If no exact match, try matching by account battle tag
                if (character == null)
                {
                    character = characters.FirstOrDefault(c =>
                        c.Members?.Character?.Name?.Contains(nickname, StringComparison.OrdinalIgnoreCase) ?? false);
                }

                // Final fallback: use first result if it has team data
                if (character == null)
                {
                    character = characters.FirstOrDefault(c => c.Members?.Character != null);
                }

                if (character?.Members?.Character == null)
                {
                    _logger.Warning("Character found but no member/character data for {BattleTag}", battleTag);
                    _logger.Debug("Available characters: {@Characters}", characters.Select(c => new { c.Members?.Character?.Name, c.Members?.Character?.Id }).ToList());
                    return null;
                }

                var characterId = character.Members.Character.Id;
                var characterName = character.Members.Character.Name;
                _logger.Information("Found character {CharacterName} (ID: {CharacterId}) for {BattleTag}", characterName, characterId, battleTag);

                // Fetch seasonal character-teams data (current season only, 1v1 queue)
                var teamsData = await _client.GetCharacterTeamsSeasonAsync(characterId, currentSeason.Id, cancellationToken).ConfigureAwait(false);
                if (teamsData == null || teamsData.Count == 0)
                {
                    _logger.Warning("No teams data available for character {CharacterId} ({CharacterName}) in season {SeasonId}. Falling back to stats endpoint.",
                        characterId, characterName, currentSeason.Id);
                    return await GetPlayerStatsLegacyAsync(battleTag, cancellationToken).ConfigureAwait(false);
                }

                _logger.Debug("Retrieved {TeamCount} teams for character {CharacterId} in season {SeasonId}",
                    teamsData.Count, characterId, currentSeason.Id);

                // Extract 1v1 solo ladder entry (queueType=201, teamType=0)
                var overall1v1 = teamsData.FirstOrDefault(t =>
                    t.QueueType == 201 &&      // 1v1
                    t.TeamType == 0);          // solo

                if (overall1v1 == null)
                {
                    _logger.Warning("No 1v1 solo entry found in teams data for character {CharacterId}", characterId);
                    return await GetPlayerStatsLegacyAsync(battleTag, cancellationToken).ConfigureAwait(false);
                }

                var currentRating = overall1v1.Rating ?? 0;
                var currentLeague = overall1v1.LeagueType;
                var league = ConvertLeagueType(currentLeague);
                var totalGames = (overall1v1.Wins ?? 0) + (overall1v1.Losses ?? 0);

                _logger.Information("Retrieved SC2Pulse 1v1 stats for {BattleTag}: Current League={CurrentLeagueRaw} ({LeagueName}) MMR={MMR} Wins={Wins} Losses={Losses}",
                    battleTag, currentLeague, league, currentRating, overall1v1.Wins, overall1v1.Losses);

                // Extract race-specific stats from teams data
                var raceStats = ExtractRaceStatsFromTeams(teamsData);

                var result = new SC2PulseStats(
                    Nickname: characterName,
                    CurrentLeague: league,
                    CurrentMMR: currentRating,
                    TotalGamesPlayed: totalGames,
                    HighestMMR: overall1v1.Rating ?? currentRating,
                    HighestLeague: league,  // In seasonal data, current = highest
                    RaceStats: raceStats);

                _logger.Information("Successfully built SC2PulseStats for {BattleTag}: {LeagueName} {MMR} MMR, {TotalGames} games",
                    battleTag, result.CurrentLeague, result.CurrentMMR, result.TotalGamesPlayed);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch SC2Pulse stats for {BattleTag}", battleTag);
                return null;
            }
        }

        /// <summary>
        /// Fallback method using the stats/full endpoint when character-teams data is unavailable.
        /// </summary>
        private async Task<SC2PulseStats?> GetPlayerStatsLegacyAsync(string battleTag, CancellationToken cancellationToken = default)
        {
            try
            {
                var parts = battleTag.Split('#');
                var nickname = parts[0];

                var query = new CharacterFindQuery { Query = nickname };
                var characters = await _client.FindCharactersAsync(query, cancellationToken).ConfigureAwait(false);

                if (characters == null || characters.Count == 0)
                    return null;

                var character = characters.FirstOrDefault(c =>
                    c.Members?.Character?.Name?.Equals(battleTag, StringComparison.OrdinalIgnoreCase) ?? false)
                    ?? characters.FirstOrDefault(c =>
                        c.Members?.Character?.Name?.Contains(nickname, StringComparison.OrdinalIgnoreCase) ?? false)
                    ?? characters.FirstOrDefault(c => c.Members?.Character != null);

                if (character?.Members?.Character == null)
                    return null;

                var characterId = character.Members.Character.Id;
                var characterName = character.Members.Character.Name;

                var statsArray = await _client.GetCharacterFullStatsAsync(characterId, cancellationToken).ConfigureAwait(false);
                if (statsArray == null || statsArray.Count == 0)
                {
                    _logger.Warning("No stats data available for character {CharacterId} ({CharacterName})", characterId, characterName);
                    return null;
                }

                var overall1v1Stats = statsArray.FirstOrDefault(s =>
                    s.Stats?.QueueType == 201 &&
                    s.Stats?.TeamType == 0 &&
                    s.Stats?.Race == null);

                if (overall1v1Stats?.Stats == null)
                {
                    _logger.Warning("No 1v1 overall stats found for character {CharacterId} ({CharacterName})", characterId, characterName);
                    return null;
                }

                var overallStats = overall1v1Stats.Stats;
                var currentStats = overall1v1Stats.CurrentStats;

                var currentRating = currentStats?.Rating;
                var currentLeague = currentStats?.League;
                var league = ConvertLeagueType(currentLeague);
                var mmr = currentRating ?? 0;
                var totalGames = overallStats.GamesPlayed ?? 0;

                _logger.Information("Retrieved SC2Pulse stats (legacy) for {BattleTag}: Current League={CurrentLeagueRaw} ({LeagueName}) MMR={MMR}",
                    battleTag, currentLeague, league, mmr);

                var raceStats = ExtractRaceStatsLegacy(statsArray);

                var result = new SC2PulseStats(
                    Nickname: characterName,
                    CurrentLeague: league,
                    CurrentMMR: mmr,
                    TotalGamesPlayed: totalGames,
                    HighestMMR: overallStats.RatingMax ?? mmr,
                    HighestLeague: ConvertLeagueType(overallStats.LeagueMax),
                    RaceStats: raceStats);

                return result;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to fetch SC2Pulse stats using legacy endpoint for {BattleTag}", battleTag);
                return null;
            }
        }

        /// <summary>
        /// Extracts race-specific stats from character-teams data (seasonal).
        /// </summary>
        private WinRateByRace ExtractRaceStatsFromTeams(List<CharacterTeamStats> teamsData)
        {
            if (teamsData == null || teamsData.Count == 0)
            {
                _logger.Warning("No teams data for race extraction");
                return new WinRateByRace(
                    Protoss: new WinRate(0, 0),
                    Terran: new WinRate(0, 0),
                    Zerg: new WinRate(0, 0));
            }

            // Filter to 1v1 solo entries only
            var solo1v1 = teamsData.Where(t => t.QueueType == 201 && t.TeamType == 0).ToList();

            // Get the race-keyed team entries (members contain raceGames info)
            var terranTeam = solo1v1.FirstOrDefault(t =>
                t.Members?.FirstOrDefault()?.TerranGamesPlayed > 0);
            var protossTeam = solo1v1.FirstOrDefault(t =>
                t.Members?.FirstOrDefault()?.ProtossGamesPlayed > 0);
            var zergTeam = solo1v1.FirstOrDefault(t =>
                t.Members?.FirstOrDefault()?.ZergGamesPlayed > 0);

            var terranWins = terranTeam?.Wins ?? 0;
            var terranLosses = terranTeam?.Losses ?? 0;

            var protossWins = protossTeam?.Wins ?? 0;
            var protossLosses = protossTeam?.Losses ?? 0;

            var zergWins = zergTeam?.Wins ?? 0;
            var zergLosses = zergTeam?.Losses ?? 0;

            _logger.Debug("Seasonal 1v1 race stats from teams - Terran: {TerranWins}W/{TerranLosses}L, Protoss: {ProtossWins}W/{ProtossLosses}L, Zerg: {ZergWins}W/{ZergLosses}L",
                terranWins, terranLosses, protossWins, protossLosses, zergWins, zergLosses);

            return new WinRateByRace(
                Protoss: new WinRate(protossWins, protossLosses),
                Terran: new WinRate(terranWins, terranLosses),
                Zerg: new WinRate(zergWins, zergLosses));
        }

        /// <summary>
        /// Extracts race-specific win rates from the stat entries array (legacy stats/full endpoint).
        /// </summary>
        private WinRateByRace ExtractRaceStatsLegacy(List<PlayerStatEntry> statsArray)
        {
            if (statsArray == null || statsArray.Count == 0)
            {
                _logger.Warning("No stat entries found for race stats extraction");
                return new WinRateByRace(
                    Protoss: new WinRate(0, 0),
                    Terran: new WinRate(0, 0),
                    Zerg: new WinRate(0, 0));
            }

            // Get the overall 1v1 stats first (queueType=201, teamType=0, race=null)
            var overall1v1 = statsArray.FirstOrDefault(s =>
                s.Stats?.QueueType == 201 &&
                s.Stats?.TeamType == 0 &&
                s.Stats?.Race == null);

            if (overall1v1?.Stats == null)
            {
                _logger.Warning("No overall 1v1 stats found for win rate calculation");
                return new WinRateByRace(
                    Protoss: new WinRate(0, 0),
                    Terran: new WinRate(0, 0),
                    Zerg: new WinRate(0, 0));
            }

            // Use current season stats for win rate calculation
            var overallCurrentStats = overall1v1.CurrentStats;
            var overallCurrentGames = overallCurrentStats?.GamesPlayed ?? 0;
            var overallCurrentRating = overallCurrentStats?.Rating ?? 0;

            _logger.Debug("Overall 1v1 current season - Games: {Games}, Rating: {Rating}", overallCurrentGames, overallCurrentRating);

            // Get 1v1 solo stats for each race (queueType=201, teamType=0)
            var terranEntry = statsArray.FirstOrDefault(s =>
                s.Stats?.QueueType == 201 &&
                s.Stats?.TeamType == 0 &&
                s.Stats?.Race == "TERRAN");

            var protossEntry = statsArray.FirstOrDefault(s =>
                s.Stats?.QueueType == 201 &&
                s.Stats?.TeamType == 0 &&
                s.Stats?.Race == "PROTOSS");

            var zergEntry = statsArray.FirstOrDefault(s =>
                s.Stats?.QueueType == 201 &&
                s.Stats?.TeamType == 0 &&
                s.Stats?.Race == "ZERG");

            // Extract CURRENT SEASON game counts from race-specific stats
            var terranCurrentGames = terranEntry?.CurrentStats?.GamesPlayed ?? 0;
            var protossCurrentGames = protossEntry?.CurrentStats?.GamesPlayed ?? 0;
            var zergCurrentGames = zergEntry?.CurrentStats?.GamesPlayed ?? 0;

            _logger.Debug("1v1 Current Season race games - Terran: {Terran}, Protoss: {Protoss}, Zerg: {Zerg}",
                terranCurrentGames, protossCurrentGames, zergCurrentGames);

            // Calculate wins based on overall rating in current season
            // Higher rating typically means better win rate
            // Use rating as a proxy: rating/5000 gives us a win rate estimate (clamp 10-90%)
            if (overallCurrentGames > 0 && overallCurrentRating > 0)
            {
                var winRateEstimate = Math.Min(0.9, Math.Max(0.1, overallCurrentRating / 5000.0));

                var terranWins = (int)Math.Round(terranCurrentGames * winRateEstimate);
                var terranLosses = Math.Max(0, terranCurrentGames - terranWins);

                var protossWins = (int)Math.Round(protossCurrentGames * winRateEstimate);
                var protossLosses = Math.Max(0, protossCurrentGames - protossWins);

                var zergWins = (int)Math.Round(zergCurrentGames * winRateEstimate);
                var zergLosses = Math.Max(0, zergCurrentGames - zergWins);

                _logger.Debug("Current season 1v1 race win rates (estimate={WinRate:P0}) - Terran: {TerranWins}W/{TerranLosses}L, Protoss: {ProtossWins}W/{ProtossLosses}L, Zerg: {ZergWins}W/{ZergLosses}L",
                    winRateEstimate, terranWins, terranLosses, protossWins, protossLosses, zergWins, zergLosses);

                return new WinRateByRace(
                    Protoss: new WinRate(protossWins, protossLosses),
                    Terran: new WinRate(terranWins, terranLosses),
                    Zerg: new WinRate(zergWins, zergLosses));
            }

            // Fallback to all-time stats if current season data unavailable
            var totalGames = overall1v1.Stats.GamesPlayed ?? 0;
            var totalRating = overall1v1.Stats.Rating ?? 0;

            if (totalGames > 0 && totalRating > 0)
            {
                var winRateEstimate = Math.Min(0.9, Math.Max(0.1, totalRating / 5000.0));

                var terranAllTimeGames = terranEntry?.Stats?.GamesPlayed ?? 0;
                var terranWins = (int)Math.Round(terranAllTimeGames * winRateEstimate);
                var terranLosses = Math.Max(0, terranAllTimeGames - terranWins);

                var protossAllTimeGames = protossEntry?.Stats?.GamesPlayed ?? 0;
                var protossWins = (int)Math.Round(protossAllTimeGames * winRateEstimate);
                var protossLosses = Math.Max(0, protossAllTimeGames - protossWins);

                var zergAllTimeGames = zergEntry?.Stats?.GamesPlayed ?? 0;
                var zergWins = (int)Math.Round(zergAllTimeGames * winRateEstimate);
                var zergLosses = Math.Max(0, zergAllTimeGames - zergWins);

                _logger.Debug("Using all-time stats (estimate={WinRate:P0})", winRateEstimate);

                return new WinRateByRace(
                    Protoss: new WinRate(protossWins, protossLosses),
                    Terran: new WinRate(terranWins, terranLosses),
                    Zerg: new WinRate(zergWins, zergLosses));
            }

            // No usable data
            _logger.Warning("No usable rating data for win rate estimation");
            return new WinRateByRace(
                Protoss: new WinRate(0, 0),
                Terran: new WinRate(0, 0),
                Zerg: new WinRate(0, 0));
        }

        /// <summary>
        /// Converts SC2Pulse league type integer to human-readable league name.
        /// </summary>
        private string? ConvertLeagueType(int? leagueType)
        {
            return leagueType switch
            {
                0 => "BRONZE",
                1 => "SILVER",
                2 => "GOLD",
                3 => "PLATINUM",
                4 => "DIAMOND",
                5 => "MASTER",
                6 => "GRANDMASTER",
                _ => "UNKNOWN"
            };
        }

        public void Dispose()
        {
            _client?.Dispose();
            _seasonManager?.Dispose();
        }
    }
}
