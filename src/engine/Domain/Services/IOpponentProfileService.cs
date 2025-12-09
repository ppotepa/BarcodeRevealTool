using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public interface IOpponentProfileService
    {
        OpponentProfile BuildProfile(string yourTag, string opponentTag);
    }
}
