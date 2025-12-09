using System.Threading;
using System.Threading.Tasks;

namespace BarcodeRevealTool.Engine.Application.Abstractions
{
    public interface IGameStateMonitor
    {
        ToolState CurrentState { get; }
        event EventHandler<ToolStateChangedEventArgs>? StateChanged;
        Task RunAsync(CancellationToken cancellationToken);
    }
}
