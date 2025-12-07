using BarcodeRevealTool.game.lobbies;
using Sc2Pulse;
using Sc2Pulse.Models;

namespace BarcodeRevealTool.Game
{
    /// <summary>
    /// as of now we wanna just support 1v1 mode, prolly the structure is a bit different for team games, also arcase,
    /// would need dig deeper into the lobby file structure
    /// </summary>
    internal class GameLobby : ISoloGameLobby
    {
        public Team? Team1 { get; init; }

        public Team? Team2 { get; init; }

        public Func<ISoloGameLobby, Team>? UsersTeam { get; init; }
        public Func<ISoloGameLobby, Team>? OppositeTeam { get; init; }
        public Sc2PulseClient Client { get; } = new Sc2PulseClient();
        public LadderDistinctCharacter? AdditionalData { get; private set; }
        public BuildOrderEntry? LastBuildOrderEntry { get; set; }


        public async Task EnsureAdditionalDataLoadedAsync()
        {
            if (AdditionalData != null)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobby] Additional data already loaded");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[GameLobby] Loading additional data from Sc2Pulse API");
            try
            {
                var opponentTag = OppositeTeam?.Invoke(this)?.Players.FirstOrDefault()?.Tag;
                System.Diagnostics.Debug.WriteLine($"[GameLobby] Looking up opponent: {opponentTag}");
                if (!string.IsNullOrEmpty(opponentTag))
                {
                    // Use timeout to prevent indefinite waits on slow/offline API
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    System.Diagnostics.Debug.WriteLine($"[GameLobby] Querying Sc2Pulse API with 5s timeout");
                    var result = await Client.FindCharactersAsync(
                        new Sc2Pulse.Queries.CharacterFindQuery() { Query = opponentTag },
                        cts.Token);

                    if (result.Any())
                    {
                        AdditionalData = result[0];
                        System.Diagnostics.Debug.WriteLine($"[GameLobby] Opponent data loaded: Rating={AdditionalData?.RatingMax}, League={AdditionalData?.LeagueMax}, GamesPlayed={AdditionalData?.TotalGamesPlayed}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[GameLobby] No results found for opponent tag: {opponentTag}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[GameLobby] Opponent tag is empty");
                }
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobby] Sc2Pulse API query timed out (5s limit exceeded)");
                // If loading fails (timeout, network error, etc.), AdditionalData stays null
                // UI will show "MMR data unavailable" but won't block user
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLobby] Exception in EnsureAdditionalDataLoadedAsync: {ex}");
                // If loading fails (timeout, network error, etc.), AdditionalData stays null
                // UI will show "MMR data unavailable" but won't block user
            }
        }
    }
}

