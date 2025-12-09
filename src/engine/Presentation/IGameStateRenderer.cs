using BarcodeRevealTool.Engine.Game.Lobbies;

namespace BarcodeRevealTool.Engine.Presentation
{
    public interface IGameStateRenderer
    {
        void RenderAwaitingState();
        void RenderInGameState(ISoloGameLobby lobby);
    }
}
