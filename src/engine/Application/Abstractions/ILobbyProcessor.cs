using BarcodeRevealTool.Engine.Game.Lobbies;

namespace BarcodeRevealTool.Engine.Application.Abstractions
{
    public interface ILobbyProcessor
    {
        Task<ISoloGameLobby?> TryReadLobbyAsync(CancellationToken cancellationToken);
    }
}
