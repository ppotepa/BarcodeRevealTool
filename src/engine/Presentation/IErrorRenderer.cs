namespace BarcodeRevealTool.Engine.Presentation
{
    public interface IErrorRenderer
    {
        void RenderWarning(string message);
        void RenderError(string message);
    }
}
