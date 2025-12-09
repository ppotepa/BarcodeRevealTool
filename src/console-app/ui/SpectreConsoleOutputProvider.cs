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
            // Extract nickname from tag (part before the #)
            var nickname = profile.OpponentTag.Split('#').FirstOrDefault() ?? "Unknown";

            var panel = new Panel(
                $"[yellow]Opponent:[/] {Escape(nickname)} [grey](Nick: {Escape(nickname)}, BattleTag: {Escape(profile.OpponentTag)})[/]{Environment.NewLine}" +
                $"[yellow]Win Rate:[/] {profile.VersusYou.Display}{Environment.NewLine}" +
                $"[yellow]Preferred Race:[/] {Escape(profile.PreferredRaces.Primary)}{Environment.NewLine}" +
                $"[yellow]Preferred Maps:[/] {string.Join(", ", profile.FavoriteMaps)}{Environment.NewLine}" +
                $"[yellow]Last Met:[/] {profile.LastPlayed:g}");
            panel.Header = new PanelHeader("Opponent Profile", Justify.Center);
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
