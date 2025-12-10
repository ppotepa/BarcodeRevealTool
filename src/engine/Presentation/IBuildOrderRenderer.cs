using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.Engine.Presentation
{
    public interface IBuildOrderRenderer
    {
        void RenderBuildOrder(IReadOnlyList<BuildOrderStep> steps);
        void RenderBuildPattern(BuildOrderPattern pattern);
    }
}
