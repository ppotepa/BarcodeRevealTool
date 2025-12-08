using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.game.lobbies;
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

            int barLength = 20;
            int filledLength = (int)(barLength * percent);
            string bar = new string('█', filledLength) + new string('░', barLength - filledLength);

            // Build plain text version for console output
            string progressText = $"▓ {current}/{total} {bar} {(percent * 100):F0}%";

            // Use carriage return to update on same line
            System.Console.Write($"\r[!] Caching: {progressText,-75}");
            System.Console.Out.Flush();

            System.Diagnostics.Debug.WriteLine($"[RenderCacheProgress] {current}/{total} ({(percent * 100):F0}%)");
        }

        public void RenderCacheComplete()
        {
            AnsiConsole.MarkupLine("\n[green][[+]][/] [green]Cache population complete.[/]");
        }

        public void RenderSyncComplete(int newReplays)
        {
            AnsiConsole.MarkupLine($"[green][[+]][/] [green]Synced {newReplays} new replays from disk.[/]");
        }

        public void RenderWarning(string message)
        {
            AnsiConsole.MarkupLine($"[yellow]![/] [yellow]{EscapeMarkup(message)}[/]");
        }

        public void RenderError(string message)
        {
            AnsiConsole.MarkupLine($"[red][[-]][/] [red]{EscapeMarkup(message)}[/]");
        }

        public void HandlePeriodicStateUpdate(string state, ISoloGameLobby? lobby)
        {
            // Periodic updates called every 1500ms
            // Can be used for animations, status refresh, etc.
            // Currently a no-op - can be enhanced with spinner/pulse effects while awaiting
        }

        public void RenderLobbyInfo(ISoloGameLobby lobby, object? additionalData, object? lastBuildOrder, Player? opponentPlayer = null,
            List<(string yourName, string opponentName, string yourRace, string opponentRace, DateTime gameDate, string map)>? opponentGames = null,
            List<(double timeSeconds, string kind, string name)>? opponentLastBuild = null)
        {
            System.Diagnostics.Debug.WriteLine($"[SpectreConsoleOutputProvider] RenderLobbyInfo called with lobby={lobby != null}, additionalData={additionalData != null}, lastBuildOrder={lastBuildOrder != null}, opponentPlayer={opponentPlayer != null}");
            System.Diagnostics.Debug.WriteLine($"[SpectreConsoleOutputProvider] Team1={lobby?.Team1}, Team2={lobby?.Team2}");

            AnsiConsole.Clear();

            // Title
            AnsiConsole.MarkupLine("[bold cyan]═══ LOBBY INFORMATION ═══[/]");
            AnsiConsole.WriteLine();

            // Players section - safely cast object properties to Team type
            RenderTeamInfo("Team 1", lobby?.Team1 as Team);
            AnsiConsole.WriteLine();
            RenderTeamInfo("Team 2", lobby?.Team2 as Team);
            AnsiConsole.WriteLine();

            // Opponent stats
            RenderOpponentStats(additionalData as LadderDistinctCharacter, opponentPlayer);
            AnsiConsole.WriteLine();

            // Opponent games vs you
            RenderOpponentGamesStats(opponentGames);
            AnsiConsole.WriteLine();

            // Opponent's last build order
            RenderOpponentLastBuildOrder(opponentLastBuild);
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

        private static void RenderOpponentStats(LadderDistinctCharacter? stats, Player? opponentPlayer = null)
        {
            if (stats == null)
            {
                System.Diagnostics.Debug.WriteLine($"[SpectreConsoleOutputProvider] No opponent stats available. Likely Sc2Pulse character lookup returned no results. Opponent tag used: {opponentPlayer?.Tag ?? "null"}");
                AnsiConsole.MarkupLine("[grey]No opponent data available[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Opponent Info[/]");

            // Player Details Section
            if (stats.Members?.Character != null)
            {
                var playerChar = stats.Members.Character;

                // Use opponent player's nickname from lobby if available, otherwise use SC2Pulse name
                string displayName = opponentPlayer?.NickName ?? playerChar.Name ?? "Unknown";
                AnsiConsole.MarkupLine($"[yellow]Name:[/] {displayName}");

                // Format toon handle from character data
                // Format: region-S2-realm-id (e.g., 2-S2-1-11057632)
                var toonHandle = $"{(int)playerChar.Region}-S2-{playerChar.Realm}-{playerChar.BattleNetId}";
                AnsiConsole.MarkupLine($"[yellow]Toon Handle:[/] {toonHandle}");

                // SC2 Profile URL
                var profileUrl = $"https://starcraft2.com/profile/{(int)playerChar.Region}/{playerChar.Realm}/{playerChar.BattleNetId}";
                AnsiConsole.MarkupLine($"[yellow]Profile:[/] [link]{profileUrl}[/]");

                // BattleTag on SC2Pulse
                AnsiConsole.MarkupLine($"[yellow]Character ID:[/] {playerChar.Id}");

                AnsiConsole.WriteLine();
            }

            AnsiConsole.MarkupLine("[bold cyan]Statistics[/]");

            var statsTable = new Table();
            statsTable.Border = TableBorder.Square;
            statsTable.AddColumn("[cyan]Metric[/]");
            statsTable.AddColumn("[cyan]Value[/]");

            // Current Season Stats
            if (stats.CurrentStats?.Rating.HasValue == true && stats.CurrentStats.Rating > 0)
            {
                statsTable.AddRow("Current MMR", $"[green]{stats.CurrentStats.Rating}[/]");
            }

            if (stats.CurrentStats?.Rank != null)
            {
                statsTable.AddRow("Current Rank", $"[yellow]{stats.CurrentStats.Rank}[/]");
            }

            if (stats.CurrentStats?.GamesPlayed > 0)
            {
                statsTable.AddRow("Current Games", $"[cyan]{stats.CurrentStats.GamesPlayed}[/]");
            }

            // Career High Stats
            if (stats.RatingMax > 0)
            {
                statsTable.AddRow("Peak MMR", $"[magenta]{stats.RatingMax}[/]");
            }

            if (stats.LeagueMax != null)
            {
                statsTable.AddRow("Peak League", $"[magenta]{stats.LeagueMax}[/]");
            }

            // Previous Season Stats
            if (stats.PreviousStats?.Rating > 0)
            {
                statsTable.AddRow("Previous MMR", $"[grey]{stats.PreviousStats.Rating}[/]");
            }

            if (stats.PreviousStats?.Rank != null)
            {
                statsTable.AddRow("Previous Rank", $"[grey]{stats.PreviousStats.Rank}[/]");
            }

            if (stats.PreviousStats?.GamesPlayed > 0)
            {
                statsTable.AddRow("Previous Games", $"[grey]{stats.PreviousStats.GamesPlayed}[/]");
            }

            // Total Career Stats
            if (stats.TotalGamesPlayed > 0)
            {
                statsTable.AddRow("Total Games (Career)", $"[blue]{stats.TotalGamesPlayed}[/]");
            }

            AnsiConsole.Write(statsTable);
        }

        private static void RenderOpponentGamesStats(List<(string yourName, string opponentName, string yourRace, string opponentRace, DateTime gameDate, string map)>? games)
        {
            if (games == null || games.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SpectreConsoleOutputProvider] No previous games vs opponent (games collection is null or empty after DB queries).");
                AnsiConsole.MarkupLine("[grey]No previous games vs opponent[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Head-to-Head Record[/]");

            var table = new Table();
            table.Border = TableBorder.Square;
            table.AddColumn("[cyan]Metric[/]");
            table.AddColumn("[cyan]Value[/]");

            table.AddRow("Total Games", $"[yellow]{games.Count}[/]");
            table.AddRow("Last Match", $"[cyan]{(int)(DateTime.Now - games.First().gameDate).TotalDays}d ago[/]");

            // Show recent games
            if (games.Count > 0)
            {
                var recentGame = games.First();
                table.AddRow("Most Recent Map", $"[magenta]{EscapeMarkup(recentGame.map)}[/]");
                table.AddRow("Your Last Race", $"[green]{EscapeMarkup(recentGame.yourRace)}[/]");
                table.AddRow("Their Last Race", $"[magenta]{EscapeMarkup(recentGame.opponentRace)}[/]");
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            // Show last 5 matches
            RenderLast5Matches(games);
        }

        private static void RenderLast5Matches(List<(string yourName, string opponentName, string yourRace, string opponentRace, DateTime gameDate, string map)> games)
        {
            AnsiConsole.MarkupLine("[bold cyan]Last 5 Matches[/]");

            var matchTable = new Table();
            matchTable.Border = TableBorder.Square;
            matchTable.AddColumn("[cyan]Date[/]");
            matchTable.AddColumn("[cyan]Map[/]");
            matchTable.AddColumn("[cyan]Your Race[/]");
            matchTable.AddColumn("[cyan]Their Race[/]");
            matchTable.AddColumn("[cyan]Result[/]");

            var last5 = games.Take(5).ToList();
            foreach (var game in last5)
            {
                var daysAgo = (int)(DateTime.Now - game.gameDate).TotalDays;
                var dateStr = daysAgo == 0 ? "Today" : $"{daysAgo}d ago";

                // For now, we'll use a placeholder "?" for result since winner info isn't in DB yet
                // TODO: Extract winner from replay file
                string result = "[yellow]?[/]";

                matchTable.AddRow(
                    dateStr,
                    $"[magenta]{EscapeMarkup(game.map)}[/]",
                    $"[green]{EscapeMarkup(game.yourRace)}[/]",
                    $"[cyan]{EscapeMarkup(game.opponentRace)}[/]",
                    result
                );
            }

            AnsiConsole.Write(matchTable);
        }

        private static void RenderOpponentLastBuildOrder(List<(double timeSeconds, string kind, string name)>? buildOrder)
        {
            if (buildOrder == null || buildOrder.Count == 0)
            {
                System.Diagnostics.Debug.WriteLine("[SpectreConsoleOutputProvider] No cached build order for opponent (GetOpponentLastBuildOrder returned null or empty).");
                AnsiConsole.MarkupLine("[grey]No cached build order for opponent[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Opponent's Last Build Order[/]");

            var table = new Table();
            table.Border = TableBorder.Square;
            table.AddColumn("[cyan]Time[/]");
            table.AddColumn("[cyan]Type[/]");
            table.AddColumn("[cyan]Building/Unit[/]");

            // Show up to first 10 build order items
            foreach (var entry in buildOrder.Take(10))
            {
                var timeSpan = TimeSpan.FromSeconds(entry.timeSeconds);
                string buildColor = GetBuildTypeColor(entry.kind);

                table.AddRow(
                    $"[yellow]{timeSpan:mm\\:ss}[/]",
                    $"[{buildColor}]{entry.kind}[/]",
                    EscapeMarkup(entry.name)
                );
            }

            AnsiConsole.Write(table);
        }

        private static void RenderLastBuildOrder(BuildOrderEntry? buildOrder)
        {
            AnsiConsole.MarkupLine("[bold cyan]Last Build Order[/]");

            if (buildOrder == null)
            {
                System.Diagnostics.Debug.WriteLine("[SpectreConsoleOutputProvider] No last build order data available (lobby.LastBuildOrderEntry is null).");
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

        public void RenderOpponentMatchHistory(List<(string opponentName, DateTime gameDate, string map, string yourRace, string opponentRace, string replayFileName)> history)
        {
            if (history == null || history.Count == 0)
            {
                AnsiConsole.MarkupLine("[grey]No match history found against this opponent[/]");
                return;
            }

            AnsiConsole.MarkupLine("[bold cyan]Match History[/]");

            var table = new Table();
            table.Border = TableBorder.Square;
            table.AddColumn("[cyan]Days Ago[/]");
            table.AddColumn("[cyan]Map[/]");
            table.AddColumn("[cyan]Your Race[/]");
            table.AddColumn("[cyan]Opponent Race[/]");
            table.AddColumn("[cyan]Replay File[/]");

            foreach (var (opponentName, gameDate, map, yourRace, opponentRace, replayFileName) in history)
            {
                var daysAgo = (int)(DateTime.Now - gameDate).TotalDays;
                var daysAgoStr = daysAgo == 0 ? "Today" : daysAgo == 1 ? "Yesterday" : $"{daysAgo}d ago";

                table.AddRow(
                    $"[yellow]{daysAgoStr}[/]",
                    EscapeMarkup(map),
                    $"[green]{EscapeMarkup(yourRace)}[/]",
                    $"[magenta]{EscapeMarkup(opponentRace)}[/]",
                    $"[cyan]{EscapeMarkup(Path.GetFileNameWithoutExtension(replayFileName))}[/]"
                );
            }

            AnsiConsole.Write(table);
        }

        private static string EscapeMarkup(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            return text.Replace("[", "[[").Replace("]", "]]");
        }
    }
}
