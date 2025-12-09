using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Application.Abstractions
{
    public interface IReplaySyncService
    {
        Task InitializeAsync(CancellationToken cancellationToken);
        Task SyncAsync(CancellationToken cancellationToken);
        CacheStatistics GetStatistics();
    }
}
