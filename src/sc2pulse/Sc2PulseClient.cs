using Sc2Pulse.Models;
using Sc2Pulse.Queries;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sc2Pulse
{
    /// <summary>
    /// SC2 Pulse API client focused on character-related endpoints.
    /// </summary>
    public sealed class Sc2PulseClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;

        public Sc2PulseClient()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://sc2pulse.nephest.com")
            };

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                WriteIndented = false
            };
        }

        public Uri BaseAddress => _httpClient.BaseAddress ?? throw new InvalidOperationException("HttpClient.BaseAddress is null");

        public void Dispose() => _httpClient?.Dispose();

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

        #endregion Character Endpoints
    }
}