using Sc2Pulse.Models;
using Sc2Pulse.Queries;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sc2Pulse
{
    /// <summary>
    /// SC2 Pulse API client focused on character-related endpoints.
    /// Includes request logging and retry policy for resilience.
    /// </summary>
    public sealed class Sc2PulseClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private Action<string>? _debugLog;

        // Retry policy configuration
        private const int MaxRetries = 3;
        private const int InitialDelayMs = 500;

        public Sc2PulseClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://sc2pulse.nephest.com"),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public Uri BaseAddress => _httpClient.BaseAddress ?? throw new InvalidOperationException("HttpClient.BaseAddress is null");

        /// <summary>
        /// Sets a callback function for detailed request logging.
        /// Useful for debugging API issues. The callback will be invoked with log messages.
        /// </summary>
        public void SetDebugLogger(Action<string>? logCallback)
        {
            _debugLog = logCallback;
        }

        public void Dispose() => _httpClient?.Dispose();

        /// <summary>
        /// Makes an HTTP request with detailed logging and exponential backoff retry policy.
        /// </summary>
        private async Task<HttpResponseMessage> ExecuteWithRetryAsync(
            string url,
            CancellationToken cancellationToken,
            string? description = null)
        {
            _debugLog?.Invoke($"SC2Pulse API Request: GET {url} {(description != null ? $"({description})" : "")}");

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                    sw.Stop();

                    _debugLog?.Invoke($"SC2Pulse API Response: {url} {(int)response.StatusCode} {sw.ElapsedMilliseconds}ms");

                    if (response.IsSuccessStatusCode)
                    {
                        return response;
                    }

                    // Don't retry on 4xx errors (except 429 for rate limit)
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500 && response.StatusCode != System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _debugLog?.Invoke($"SC2Pulse API Error: {url} {(int)response.StatusCode} - Not retrying client error");
                        return response;
                    }

                    if (attempt < MaxRetries)
                    {
                        var delayMs = InitialDelayMs * (int)Math.Pow(2, attempt - 1);
                        _debugLog?.Invoke($"SC2Pulse API Error: {url} {(int)response.StatusCode} - Retrying in {delayMs}ms (attempt {attempt}/{MaxRetries})");
                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return response;
                }
                catch (TaskCanceledException ex)
                {
                    _debugLog?.Invoke($"SC2Pulse API Request timeout: {url} (attempt {attempt}/{MaxRetries}) - {ex.Message}");

                    if (attempt < MaxRetries)
                    {
                        var delayMs = InitialDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw;
                }
                catch (HttpRequestException ex)
                {
                    _debugLog?.Invoke($"SC2Pulse API Request error: {url} (attempt {attempt}/{MaxRetries}) - {ex.Message}");

                    if (attempt < MaxRetries)
                    {
                        var delayMs = InitialDelayMs * (int)Math.Pow(2, attempt - 1);
                        await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    throw;
                }
            }

            throw new InvalidOperationException("Retry loop completed without result");
        }

        #region Character Endpoints

        /// <summary>
        /// GET /api/characters - Fetch characters by various filters (ID, clan, pro player, account, toon handle).
        /// </summary>
        public async Task<JsonDocument> GetCharactersAsync(CharactersQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/characters{query?.ToQueryString() ?? string.Empty}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/characters?query - Text-based character search; returns array of LadderDistinctCharacter.
        /// </summary>
        public async Task<List<LadderDistinctCharacter>?> FindCharactersAsync(CharacterFindQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/characters{query.ToQueryString()}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<LadderDistinctCharacter>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/characters?field=id&name - Advanced character search by name; returns array of IdProjectionLong.
        /// Constraint: Use either (0/1 season with multiple queues) OR (multiple seasons with 0/1 queue).
        /// </summary>
        public async Task<List<IdProjectionLong>?> GetCharacterIdsAsync(CharacterIdsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/characters?field=id&name{query.ToQueryString()}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<IdProjectionLong>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/characters/suggestions - Autocomplete/suggestion endpoint for character names.
        /// </summary>
        public async Task<List<string>?> GetCharacterSuggestionsAsync(CharacterSuggestionsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/characters/suggestions{query.ToQueryString()}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<string>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/character-teams - Fetch teams associated with characters; returns array of LadderTeam.
        /// Note: If multiple characters are used, you must supply exactly 1 season and 1 queue.
        /// </summary>
        public async Task<List<LadderTeam>?> GetCharacterTeamsAsync(CharacterTeamsQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/character-teams{query?.ToQueryString() ?? string.Empty}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<LadderTeam>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/character-matches - Fetch match history for characters; returns CursorNavigableResultList of LadderMatch.
        /// Supports pagination via before/after cursor tokens.
        /// </summary>
        public async Task<CursorNavigableResultList<LadderMatch>?> GetCharacterMatchesAsync(CharacterMatchesQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/character-matches{query?.ToQueryString() ?? string.Empty}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CursorNavigableResultList<LadderMatch>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/character-links - Fetch external links for characters (Twitch, liquipedia, etc.); returns array of ExternalLinkResolveResult.
        /// </summary>
        public async Task<List<ExternalLinkResolveResult>?> GetCharacterLinksAsync(CharacterLinksQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/character-links{query?.ToQueryString() ?? string.Empty}";
            var resp = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<ExternalLinkResolveResult>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/character/{characterId}/stats/full - Fetch complete player statistics.
        /// Returns array of stat entries with overall and race-specific stats across different queue types.
        /// Queue types: 201=1v1, 202=2v2, 203=3v3, 204=4v4
        /// Races: null=overall, "TERRAN", "PROTOSS", "ZERG"
        /// Includes automatic retry with exponential backoff on transient failures.
        /// </summary>
        public async Task<List<PlayerStatEntry>?> GetCharacterFullStatsAsync(long characterId, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/character/{characterId}/stats/full";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, $"character-full-stats for characterId={characterId}").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<PlayerStatEntry>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion Character Endpoints

        #region Season Endpoints

        /// <summary>
        /// GET /api/seasons - Fetch all seasons; returns array of Season.
        /// Each season has region-specific information (KR, EU, US, CN).
        /// Includes automatic retry with exponential backoff on transient failures.
        /// </summary>
        public async Task<List<Season>?> GetSeasonsAsync(CancellationToken cancellationToken = default)
        {
            var url = "/sc2/api/seasons";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "fetch all seasons").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<Season>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion Season Endpoints

        #region Team Endpoints

        /// <summary>
        /// GET /api/character-teams - Fetch teams for a character in current or specific season.
        /// Query parameters can include characterId, season, queueType for filtering.
        /// Returns array of team data with full player stats and win/loss records.
        /// Includes automatic retry with exponential backoff on transient failures.
        /// </summary>
        public async Task<List<CharacterTeamStats>?> GetCharacterTeamsSeasonAsync(
            long characterId,
            long seasonId,
            int queueType = 201,
            CancellationToken cancellationToken = default)
        {
            var queueFilter = queueType > 0 ? $"&queueType={queueType}" : string.Empty;
            var url = $"/sc2/api/character-teams?characterId={characterId}&season={seasonId}{queueFilter}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, $"character-teams for characterId={characterId}, season={seasonId}, queueType={queueType}").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<CharacterTeamStats>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion Team Endpoints

        #region Ladder & Team APIs

        /// <summary>
        /// GET /api/tier-thresholds - Returns tier threshold ratings grouped by region/league.
        /// </summary>
        public async Task<Dictionary<string, Dictionary<string, Dictionary<string, List<int>>>>?> GetTierThresholdsAsync(
            TierThresholdsQuery query,
            CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/tier-thresholds{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "tier-thresholds").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Dictionary<string, Dictionary<string, Dictionary<string, List<int>>>>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/teams - Raw response for legacy team lookups.
        /// </summary>
        public async Task<JsonDocument> GetTeamsAsync(TeamsQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/teams{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "teams").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/teams?recent - Fetch recent ladder teams for a queue/league combination.
        /// </summary>
        public async Task<List<LadderTeam>?> GetRecentTeamsAsync(RecentTeamsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/teams?recent{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "recent-teams").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<LadderTeam>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/teams?queue&season - Ladder listing with pagination support.
        /// </summary>
        public async Task<CursorNavigableResultList<LadderTeam>?> GetLadderAsync(LadderTeamsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/teams?queue&season{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "ladder").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CursorNavigableResultList<LadderTeam>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/teams?last - Fetch most recent team for legacy UIDs.
        /// </summary>
        public async Task<List<LadderTeam>?> GetLastTeamsAsync(LastTeamsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/teams?last{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "last-teams").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<LadderTeam>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/team-history-summaries - Aggregated team history metrics.
        /// </summary>
        public async Task<List<TeamHistorySummaryEntry>?> GetTeamHistorySummariesAsync(TeamHistorySummariesQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/team-history-summaries{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "team-history-summaries").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<TeamHistorySummaryEntry>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/team-histories - Detailed team histories.
        /// </summary>
        public async Task<List<TeamHistoryEntry>?> GetTeamHistoriesAsync(TeamHistoriesQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/team-histories{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "team-histories").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<TeamHistoryEntry>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Stream & Match Endpoints

        /// <summary>
        /// GET /api/streams - Fetch live SC2 community streams.
        /// </summary>
        public async Task<CommunityStreamResult?> GetCommunityStreamsAsync(StreamsQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/streams{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "streams").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CommunityStreamResult>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/matches?vod - Fetch ladder matches that have VODs.
        /// </summary>
        public async Task<CursorNavigableResultList<LadderMatch>?> GetVodMatchesAsync(VodMatchesQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/matches?vod{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "vod-matches").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CursorNavigableResultList<LadderMatch>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Clan Endpoints

        /// <summary>
        /// GET /api/clans - Cursor-based clan browser.
        /// </summary>
        public async Task<CursorNavigableResultList<Clan>?> GetClansAsync(ClansQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/clans{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "clans").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CursorNavigableResultList<Clan>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/clans?query - Text search for clans.
        /// </summary>
        public async Task<List<Clan>?> FindClansAsync(ClanSearchQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/clans?query{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "clan-search").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<Clan>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/clan-histories - Clan membership history feed.
        /// </summary>
        public async Task<CursorNavigableResultList<LadderClanMemberEvents>?> GetClanHistoriesAsync(ClanHistoriesQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/clan-histories{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "clan-histories").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<CursorNavigableResultList<LadderClanMemberEvents>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion

        #region Stats & Misc Endpoints

        /// <summary>
        /// GET /api/stats/player-base - Ladder population snapshot.
        /// </summary>
        public async Task<List<QueueStats>?> GetPlayerBaseStatsAsync(PlayerBaseStatsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/stats/player-base{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "player-base").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<QueueStats>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/stats/balance-reports - Map/Race/Ladder balance reports.
        /// </summary>
        public async Task<LadderMapStatsFilm?> GetBalanceReportsAsync(BalanceReportsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/stats/balance-reports{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "balance-reports").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<LadderMapStatsFilm>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/stats/activity - Ladder activity per region/league/race.
        /// </summary>
        public async Task<Dictionary<string, MergedLadderSearchStatsResult>?> GetActivityStatsAsync(ActivityStatsQuery query, CancellationToken cancellationToken = default)
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            var url = $"/sc2/api/stats/activity{query.ToQueryString()}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "activity-stats").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Dictionary<string, MergedLadderSearchStatsResult>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/patches - StarCraft II patch list with release times.
        /// </summary>
        public async Task<List<LadderPatch>?> GetPatchesAsync(PatchesQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/patches{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "patches").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<List<LadderPatch>>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// GET /api/entities - Fetch mixed entity types (characters/clans/pros/accounts).
        /// </summary>
        public async Task<Group?> GetEntitiesAsync(EntitiesQuery? query = null, CancellationToken cancellationToken = default)
        {
            var url = $"/sc2/api/entities{query?.ToQueryString() ?? string.Empty}";
            var resp = await ExecuteWithRetryAsync(url, cancellationToken, "entities").ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            return await resp.Content.ReadFromJsonAsync<Group>(_jsonOptions, cancellationToken).ConfigureAwait(false);
        }

        #endregion
    }
}
