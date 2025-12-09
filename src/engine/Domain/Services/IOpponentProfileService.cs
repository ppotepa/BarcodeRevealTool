using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Domain.Services
{
    public interface IOpponentProfileService
    {
        Task<OpponentProfile> BuildProfileAsync(string yourTag, string opponentTag, CancellationToken cancellationToken = default);
    }
}
