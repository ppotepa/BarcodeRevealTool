using BarcodeRevealTool.Game;
using Sc2Pulse.Models;

namespace BarcodeRevealTool.game.lobbies
{
    public interface ISoloGameLobby : IGameLobby
    {
        Team? Team2 { get; }
        LadderDistinctCharacter? AdditionalData { get; }
        BuildOrderEntry? LastBuildOrderEntry { get; set; }
        Task EnsureAdditionalDataLoadedAsync();
    }

    public interface IGameLobby
    {
        Team? Team1 { get; }
    }
}
