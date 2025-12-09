using System;
using System.Collections.Generic;
using System.Linq;
using BarcodeRevealTool.Engine.Domain.Models;
using BarcodeRevealTool.Engine.Game;
using BarcodeRevealTool.Engine.Game.Lobbies;
using BarcodeRevealTool.Engine.Presentation;
using Spectre.Console;

namespace BarcodeRevealTool.UI.Console
{
    internal sealed class SpectreConsoleOutputProvider :
        IGameStateRenderer,
        IMatchHistoryRenderer,
        IBuildOrderRenderer,
        IErrorRenderer
    {
        public void RenderAwaitingState()
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[yellow]Waiting for a StarCraft II lobby...[/]");
        }

        public void RenderInGameState(ISoloGameLobby lobby)
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[bold cyan]Lobby Detected[/]");
            RenderTeam("Your Team", lobby.Team1, null);
            AnsiConsole.WriteLine();
            RenderTeam("Opponent", lobby.Team2, lobby.Team1);
        }

        public void RenderMatchHistory(IReadOnlyList<MatchResult> matches, MatchStatistics statistics)
        {
            if (matches.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No previous matches recorded.[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Date");
            table.AddColumn("Map");
            table.AddColumn("Your Race");
            table.AddColumn("Opponent Race");
            table.AddColumn("Result");

            foreach (var match in matches.Take(5))
            {
                var daysAgo = (int)(DateTime.UtcNow - match.GameDate).TotalDays;
                var label = daysAgo == 0 ? "Today" : $"{daysAgo}d ago";
                table.AddRow(
                    label,
                    Escape(match.Map),
                    Escape(match.YourRace),
                    Escape(match.OpponentRace),
                    match.YouWon ? "[green]WIN[/]" : "[red]LOSS[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"Win Rate: [bold]{statistics.WinRate.Display}[/] across {statistics.GamesPlayed} games");
        }

        public void RenderOpponentProfile(OpponentProfile profile)
        {
            var content = new List<string>();

            // Player Identity Section
            var nickname = profile.OpponentTag.Split('#').FirstOrDefault() ?? "Unknown";
            content.Add($"[bold cyan]⚔ {Escape(nickname)}[/] [grey]{Escape(profile.OpponentTag)}[/]");
            content.Add("");

            // SC2Pulse Live Stats Section
            if (profile.LiveStats != null)
            {
                content.Add("[bold yellow]Current Ladder Status[/]");
                var leagueColor = profile.LiveStats.CurrentLeague switch
                {
                    "GRANDMASTER" => "[bold red]",
                    "MASTER" => "[bold magenta]",
                    "DIAMOND" => "[bold blue]",
                    "PLATINUM" => "[cyan]",
                    "GOLD" => "[yellow]",
                    "SILVER" => "[white]",
                    _ => "[grey]"
                };
                var leagueEnd = profile.LiveStats.CurrentLeague switch
                {
                    "GRANDMASTER" => "[/]",
                    "MASTER" => "[/]",
                    "DIAMOND" => "[/]",
                    "PLATINUM" => "[/]",
                    "GOLD" => "[/]",
                    "SILVER" => "[/]",
                    _ => "[/]"
                };

                var mmrDisplay = profile.LiveStats.CurrentMMR.HasValue
                    ? $"{leagueColor}{profile.LiveStats.CurrentLeague}{leagueEnd} {profile.LiveStats.CurrentMMR} MMR"
                    : "[grey]Not ranked[/]";

                content.Add($"  League: {mmrDisplay}");
                content.Add($"  Games Played: [bold]{profile.LiveStats.TotalGamesPlayed}[/]");

                if (profile.LiveStats.HighestMMR.HasValue)
                {
                    content.Add($"  Peak: {profile.LiveStats.HighestLeague} {profile.LiveStats.HighestMMR} MMR");
                }
                content.Add("");
            }

            // Head-to-Head Record Section
            content.Add("[bold yellow]Head-to-Head Record[/]");
            content.Add($"  Versus You: [bold green]{profile.VersusYou.Wins}W[/] - [bold red]{profile.VersusYou.Losses}L[/] ({profile.VersusYou.Display})");
            content.Add("");

            // Opponent Preferences Section
            content.Add("[bold yellow]Preferred Playstyle[/]");

            // Show race stats if available from SC2Pulse
            if (profile.LiveStats?.RaceStats != null)
            {
                var raceStats = profile.LiveStats.RaceStats;

                // Main race with win/loss
                var mainRace = profile.PreferredRaces.Primary;
                content.Add($"  Main Race: [bold cyan]{Escape(mainRace)}[/]");

                // Show win rates for each race
                var terranDisplay = $"Terran: {raceStats.Terran.Wins}W-{raceStats.Terran.Losses}L";
                var protossDisplay = $"Protoss: {raceStats.Protoss.Wins}W-{raceStats.Protoss.Losses}L";
                var zergDisplay = $"Zerg: {raceStats.Zerg.Wins}W-{raceStats.Zerg.Losses}L";

                content.Add($"    ({terranDisplay} | {protossDisplay} | {zergDisplay})");
            }
            else
            {
                content.Add($"  Main Race: {Escape(profile.PreferredRaces.Primary)}");
            }

            if (!string.IsNullOrEmpty(profile.PreferredRaces.Secondary))
            {
                content.Add($"  Secondary: {Escape(profile.PreferredRaces.Secondary)}");
            }
            if (!string.IsNullOrEmpty(profile.PreferredRaces.Tertiary))
            {
                content.Add($"  Tertiary: {Escape(profile.PreferredRaces.Tertiary)}");
            }
            content.Add("");

            // Opponent Preferences Section
            content.Add("[bold yellow]Preferred Playstyle[/]");
            content.Add($"  Main Race: {Escape(profile.PreferredRaces.Primary)}");
            if (!string.IsNullOrEmpty(profile.PreferredRaces.Secondary))
            {
                content.Add($"  Secondary: {Escape(profile.PreferredRaces.Secondary)}");
            }
            if (!string.IsNullOrEmpty(profile.PreferredRaces.Tertiary))
            {
                content.Add($"  Tertiary: {Escape(profile.PreferredRaces.Tertiary)}");
            }
            content.Add("");

            // Recent Activity Section
            if (profile.LastPlayed != DateTime.MinValue)
            {
                var daysAgo = (int)(DateTime.UtcNow - profile.LastPlayed).TotalDays;
                var timeDisplay = daysAgo == 0 ? "Today" : daysAgo == 1 ? "Yesterday" : $"{daysAgo}d ago";
                content.Add("[bold yellow]Recent Activity[/]");
                content.Add($"  Last Played: [yellow]{timeDisplay}[/]");
                content.Add("");
            }

            // Build Order Pattern Section
            if (!string.IsNullOrEmpty(profile.CurrentBuildPattern.MostFrequentBuild))
            {
                content.Add("[bold yellow]Favorite Opening[/]");
                content.Add($"  Build: [cyan]{Escape(profile.CurrentBuildPattern.MostFrequentBuild)}[/]");
                content.Add($"  Last Observed: {profile.CurrentBuildPattern.LastAnalyzed:g}");
            }

            var panel = new Panel(string.Join(Environment.NewLine, content));
            panel.Header = new PanelHeader("[bold]⚔ OPPONENT PROFILE[/]", Justify.Center);
            panel.Border = BoxBorder.Rounded;
            panel.Expand = true;

            AnsiConsole.Write(panel);
        }

        public void RenderBuildOrder(IReadOnlyList<BuildOrderStep> steps)
        {
            if (steps.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No build order cached for this opponent.[/]");
                return;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Time");
            table.AddColumn("Type");
            table.AddColumn("Name");

            foreach (var step in steps.Take(10))
            {
                table.AddRow(
                    step.Time.ToString(@"mm\:ss"),
                    Escape(step.Kind),
                    Escape(step.Name));
            }

            AnsiConsole.Write(table);
        }

        public void RenderBuildPattern(BuildOrderPattern pattern)
        {
            AnsiConsole.MarkupLine($"[bold cyan]Build Trend:[/] {Escape(pattern.MostFrequentBuild)} (updated {pattern.LastAnalyzed:g})");
        }

        public void RenderWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow]WARNING:[/] {Escape(message)}");
        }

        public void RenderError(string message)
        {
            AnsiConsole.MarkupLine($"[red]ERROR:[/] {Escape(message)}");
        }

        private static void RenderTeam(string title, Team team, Team? otherTeam = null)
        {
            AnsiConsole.MarkupLine($"[bold]{Escape(title)}[/]");
            if (team.Players.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No players detected.[/]");
                return;
            }

            foreach (var player in team.Players)
            {
                // Use the actual nickname from the player object
                var displayName = player.NickName ?? "Unknown";
                var playerTag = player.Tag ?? "Unknown";
                var otherTag = otherTeam?.Players.FirstOrDefault()?.Tag ?? "Unknown";
                AnsiConsole.MarkupLine($"  [cyan]-[/] {Escape(displayName)} [grey](Nick: {Escape(displayName)}, BattleTag: {Escape(playerTag)})[/]");
            }
        }

        private static string Escape(string? text)
        {
            return string.IsNullOrEmpty(text)
                ? string.Empty
                : text.Replace("[", "[[").Replace("]", "]]");
        }
    }
}
