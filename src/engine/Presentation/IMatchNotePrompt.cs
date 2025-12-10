namespace BarcodeRevealTool.Engine.Presentation
{
    public interface IMatchNotePrompt
    {
        /// <summary>
        /// Prompt the user for a note about a completed match.
        /// Returns null or whitespace when the user skips.
        /// </summary>
        string? PromptForNote(string yourTag, string opponentTag, string mapName);
    }
}
