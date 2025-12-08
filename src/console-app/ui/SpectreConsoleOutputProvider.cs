using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Game;
using Sc2Pulse.Models;
using Spectre.Console;

namespace BarcodeRevealTool.UI.Console
{
    /// <summary>
    /// Spectre.Console implementation of IOutputProvider.
    /// Handles all colorful console rendering for the engine.
    /// </summary>
    internal class SpectreConsoleOutputProvider : IOutputProvider
    {
        public void Clear()
        {
            AnsiConsole.Clear();
        }

        public void RenderAwaitingState()
        {
            AnsiConsole.Clear();
            AnsiConsole.MarkupLine("[yellow]Waiting for game to start...[/]");
        }

        public void RenderStateChange(string from, string to)
        {
            AnsiConsole.MarkupLine($"[cyan]State Change:[/] [grey]{from}[/] CHANGED TO [green]{to}[/]");
        }

        public void RenderCacheInitializingMessage()
        {
            AnsiConsole.MarkupLine("[yellow]First startup detected.[/] Building replay cache...");
        }

        public void RenderCacheSyncMessage()
        {
            AnsiConsole.MarkupLine("[cyan]Cache found.[/] Syncing with disk...");
        }

        public void RenderCacheProgress(int current, int total)
        {
            var percent = (double)current / total;
            AnsiConsole.Markup($"\r[green]▓[/]");
            AnsiConsole.Markup($" {current}/{total} ");

            int barLength = 20;
            int filledLength = (int)(barLength * percent);
            string bar = new string('█', filledLength) + new string('░', barLength - filledLength);
            AnsiConsole.Markup($"[blue]{bar}[/] {(percent * 100):F0}%");
        }

        public void RenderCacheComplete()
        {
            AnsiConsole.MarkupLine("\n[green]✓[/] [green]Cache population complete.[/]");
        }

        public void RenderSyncComplete(int newReplays)
        {
            AnsiConsole.MarkupLine($"[green]✓[/] [green]Synced {newReplays} new replays from disk.[/]");
        }

        public void RenderWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠[/] [yellow]{EscapeMarkup(message)}[/]");
        }

        public void RenderError(string message)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] [red]{EscapeMarkup(message)}[/]");
        }

        public void HandlePeriodicStateUpdate(string state, ISoloGameLobby? lobby)
        {
            // Periodic updates called every 1500ms
            // Can be used for animations, status refresh, etc.
            // Currently a no-op - can be enhanced with spinner/pulse effects while awaiting
        }

        public void RenderLobbyInfo(BarcodeRevealTool.Engine.Abstractions.ISoloGameLobby lobby, object? additionalData, object? lastBuildOrder)
        {
            System.Diagnostics.Debug.WriteLine($"[SpectreConsoleOutputProvider] RenderLobbyInfo called with lobby={lobby != null}, additionalData={additionalData != null}, lastBuildOrder={lastBuildOrder != null}");
            System.Diagnostics.Debug.WriteLine($"[SpectreConsoleOutputProvider] Team1={lobby.Team1}, Team2={lobby.Team2}");
            
            AnsiConsole.Clear();

            // Title
            AnsiConsole.MarkupLine("[bold cyan]═══ LOBBY INFORMATION ═══[/]");
            AnsiConsole.WriteLine();

            // Players section - safely cast object properties to Team type
            RenderTeamInfo("Team 1", lobby.Team1 as Team);
            AnsiConsole.WriteLine();
            RenderTeamInfo("Team 2", lobby.Team2 as Team);
            AnsiConsole.WriteLine();

            // Opponent stats
            RenderOpponentStats(additionalData as LadderDistinctCharacter);
            AnsiConsole.WriteLine();

            // Last build order
            RenderLastBuildOrder(lastBuildOrder as BuildOrderEntry);
        }

        private static void RenderTeamInfo(string teamName, Team? team)
        {
            if (team == null)
            {
                AnsiConsole.MarkupLine($"[grey]{teamName}: No data[/]");
                return;
            }

            AnsiConsole.MarkupLine($"[bold yellow]{teamName}[/]");
            foreach (var player in team.Players)
            {
                AnsiConsole.MarkupLine($"  [cyan]→[/] {EscapeMarkup(player.NickName)} {EscapeMarkup(player.Tag)}");
            }
        }

        private static void RenderOpponentStats(LadderDistinctCharacter? stats)
        {
            if (stats == null)
            {
                AnsiConsole.MarkupLine("[grey]No opponent data available[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Opponent Stats[/]");

            var table = new Table();
            table.Border = TableBorder.Square;
            table.AddColumn("[cyan]Stat[/]");
            table.AddColumn("[cyan]Value[/]");

            if (stats.CurrentStats?.Rank != null)
            {
                table.AddRow("Current Rank", $"[yellow]{stats.CurrentStats.Rank}[/]");
            }

            if (stats.LeagueMax != null)
            {
                table.AddRow("Max Rank", $"[green]{stats.LeagueMax}[/]");
            }

            if (stats.CurrentStats?.Rating > 0)
            {
                table.AddRow("MMR", $"[green]{stats.CurrentStats.Rating}[/]");
            }

            if (stats.CurrentStats?.GamesPlayed > 0)
            {
                table.AddRow("Games Played", $"[yellow]{stats.CurrentStats.GamesPlayed}[/]");
            }

            AnsiConsole.Write(table);
        }

        private static void RenderLastBuildOrder(BuildOrderEntry? buildOrder)
        {
            AnsiConsole.MarkupLine("[bold cyan]Last Build Order[/]");

            if (buildOrder == null)
            {
                AnsiConsole.MarkupLine("[grey]No build order data available[/]");
                return;
            }

            var timeSpan = TimeSpan.FromSeconds(buildOrder.TimeSeconds);
            string buildColor = GetBuildTypeColor(buildOrder.Kind);

            var table = new Table();
            table.Border = TableBorder.Square;
            table.AddColumn("[cyan]Time[/]");
            table.AddColumn("[cyan]Type[/]");
            table.AddColumn("[cyan]Building/Unit[/]");

            table.AddRow(
                $"[yellow]{timeSpan:mm\\:ss}[/]",
                $"[{buildColor}]{buildOrder.Kind}[/]",
                EscapeMarkup(buildOrder.Name)
            );

            AnsiConsole.Write(table);
        }

        private static string GetBuildTypeColor(string buildType)
        {
            return buildType.ToLower() switch
            {
                "worker" => "grey",
                "army" => "red",
                "building" => "blue",
                "upgrade" => "magenta",
                _ => "white"
            };
        }

        private static string EscapeMarkup(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("[", "[[").Replace("]", "]]");
        }
    }
}
