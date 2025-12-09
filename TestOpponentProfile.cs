using System;
using BarcodeRevealTool.Engine.Domain.Models;

namespace BarcodeRevealTool.TestOpponentProfile
{
    class Program
    {
        static void Main(string[] args)
        {
            // Create a sample opponent profile with SC2Pulse data
            var sc2PulseStats = new SC2PulseStats(
                Nickname: "Alfir#285",
                CurrentLeague: "MASTER",
                CurrentMMR: 5100,
                TotalGamesPlayed: 1234,
                HighestMMR: 5200,
                HighestLeague: "MASTER",
                RaceStats: new WinRateByRace(
                    Protoss: new WinRate(45, 55),
                    Terran: new WinRate(60, 40),
                    Zerg: new WinRate(50, 50)
                )
            );

            var profile = new OpponentProfile(
                OpponentTag: "Alfir#285",
                VersusYou: new WinRate(3, 7),
                PreferredRaces: new PreferredRaces("Terran"),
                FavoriteMaps: new[] { "Lightshade", "Blackpink", "Gilneas Station" },
                CurrentBuildPattern: new BuildOrderPattern("Rax FE", DateTime.UtcNow.AddDays(-2)),
                LastPlayed: DateTime.UtcNow.AddDays(-1),
                LiveStats: sc2PulseStats
            );

            Console.WriteLine("=== OPPONENT PROFILE TEST ===");
            Console.WriteLine($"Opponent: {profile.OpponentTag}");
            Console.WriteLine($"Current League: {profile.LiveStats?.CurrentLeague}");
            Console.WriteLine($"Current MMR: {profile.LiveStats?.CurrentMMR}");
            Console.WriteLine($"Total Games: {profile.LiveStats?.TotalGamesPlayed}");
            Console.WriteLine($"Head-to-Head: {profile.VersusYou.Wins}W - {profile.VersusYou.Losses}L");
            Console.WriteLine($"Preferred Race: {profile.PreferredRaces.Primary}");
            Console.WriteLine($"Last Played: {(DateTime.UtcNow - profile.LastPlayed).TotalDays:F0}d ago");
            Console.WriteLine("Profile created successfully!");
        }
    }
}
