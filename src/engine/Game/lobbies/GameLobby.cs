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
                return;

            try
            {
                var opponentTag = OppositeTeam?.Invoke(this)?.Players.FirstOrDefault()?.Tag;
                if (!string.IsNullOrEmpty(opponentTag))
                {
                    var result = await Client.FindCharactersAsync(
                        new Sc2Pulse.Queries.CharacterFindQuery() { Query = opponentTag });

                    if (result.Any())
                    {
                        AdditionalData = result[0];
                    }
                }
            }
            catch
            {
                // If loading fails, AdditionalData stays null
            }
        }
    }
}
