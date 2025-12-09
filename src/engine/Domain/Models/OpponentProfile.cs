using System;
using System.Collections.Generic;

namespace BarcodeRevealTool.Engine.Domain.Models
{
    /// <summary>
    /// Snapshot of all information known about an opponent.
    /// Combines local replay cache history with SC2Pulse live statistics.
    /// </summary>
    public record OpponentProfile(
        string OpponentTag,
        WinRate VersusYou,
        PreferredRaces PreferredRaces,
        IReadOnlyList<string> FavoriteMaps,
        BuildOrderPattern CurrentBuildPattern,
        DateTime LastPlayed,
        SC2PulseStats? LiveStats = null);

    /// <summary>
    /// Live player statistics from SC2Pulse API.
    /// Represents current ladder rank, MMR, and overall win rate.
    /// </summary>
    public record SC2PulseStats(
        string? Nickname,
        string? CurrentLeague,
        int? CurrentMMR,
        int? TotalGamesPlayed,
        int? HighestMMR,
        string? HighestLeague,
        WinRateByRace RaceStats);

    /// <summary>
    /// Win rate breakdown by each race.
    /// </summary>
    public record WinRateByRace(
        WinRate Protoss,
        WinRate Terran,
        WinRate Zerg);
}
