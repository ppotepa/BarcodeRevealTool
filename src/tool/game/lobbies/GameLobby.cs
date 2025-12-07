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
        public LadderDistinctCharacter AdditionalData { get; private set; }
        public BuildOrderEntry? LastBuildOrderEntry { get; set; }

        public void PrintLobbyInfo(TextWriter writer)
        {
            if (AdditionalData is null)
            {
                AdditionalData
                    = Client.FindCharactersAsync(new Sc2Pulse.Queries.CharacterFindQuery() { Query = $"{OppositeTeam(this)!.Players.First().Tag}" })
                    .GetAwaiter().GetResult()[0]!;
            }

            writer.WriteLine("=== Lobby Information ===");
            writer.WriteLine(Team1);
            writer.WriteLine(Team2);

            writer.WriteLine($"maxRank: " + AdditionalData.LeagueMax);
            writer.WriteLine($"current rank: " + AdditionalData.CurrentStats.Rank);
            writer.WriteLine("games played: " + AdditionalData.CurrentStats.GamesPlayed);
            writer.WriteLine($"current mmr: " + AdditionalData.CurrentStats.Rating);

            writer.WriteLine($"Last build order vs you");
            if (LastBuildOrderEntry != null)
            {
                var timeSpan = TimeSpan.FromSeconds(LastBuildOrderEntry.TimeSeconds);
                writer.WriteLine($"  {timeSpan:mm\\:ss}  {LastBuildOrderEntry.Kind,-8}  {LastBuildOrderEntry.Name}");
            }
            else
            {
                writer.WriteLine("  No build order data available");
            }
        }
    }
}