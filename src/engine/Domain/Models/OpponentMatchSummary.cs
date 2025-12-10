namespace BarcodeRevealTool.Engine.Domain.Models
{
    /// <summary>
    /// Represents a single recent match fetched from SC2Pulse for the opponent.
    /// </summary>
    public record OpponentMatchSummary(
        DateTime PlayedAt,
        string MapName,
        string EnemyName,
        string EnemyRace,
        bool OpponentWon,
        TimeSpan? Duration,
        string? EnemyBattleTag,
        string? EnemyToon);
}
