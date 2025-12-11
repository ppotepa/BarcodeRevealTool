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
        IErrorRenderer,
        IMatchNotePrompt
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
            table.AddColumn("Note");

            foreach (var match in matches)
            {
                var daysAgo = (int)(DateTime.UtcNow - match.GameDate).TotalDays;
                var label = daysAgo == 0 ? "Today" : $"{daysAgo}d ago";
                table.AddRow(
                    label,
                    Escape(match.Map),
                    Escape(match.YourRace),
                    Escape(match.OpponentRace),
                    match.YouWon ? "[green]WIN[/]" : "[red]LOSS[/]",
                    Escape(match.Note ?? string.Empty));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLine($"Win Rate: [bold]{statistics.WinRate.Display}[/] across {statistics.GamesPlayed} games");
        }

        public void RenderOpponentProfile(OpponentProfile profile)
        {
            var content = new List<string>();

            // Player Identity Section - FROM LOBBY FILE
            var nickname = profile.OpponentTag.Split('#').FirstOrDefault() ?? "Unknown";
            var identityLine = $"[bold cyan]⚔ {Escape(nickname)}[/] [grey]{Escape(profile.OpponentTag)}[/]";
            if (!string.IsNullOrEmpty(profile.OpponentToon))
            {
                identityLine += $" [grey](Toon: {Escape(profile.OpponentToon)})[/]";
            }
            content.Add(identityLine);
            content.Add("");

            // Head-to-Head Record Section (from local replay cache)
            content.Add("[bold yellow]Head-to-Head Record[/]");
            content.Add($"  Versus You: [bold green]{profile.VersusYou.Wins}W[/] - [bold red]{profile.VersusYou.Losses}L[/] ({profile.VersusYou.Display})");
            content.Add("");

            // Opponent Preferences Section (from local replay cache)
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

            // Recent Activity Section (from local replay cache)
            if (profile.LastPlayed != DateTime.MinValue)
            {
                var daysAgo = (int)(DateTime.UtcNow - profile.LastPlayed).TotalDays;
                var timeDisplay = daysAgo == 0 ? "Today" : daysAgo == 1 ? "Yesterday" : $"{daysAgo}d ago";
                content.Add("[bold yellow]Recent Activity[/]");
                content.Add($"  Last Played: [yellow]{timeDisplay}[/]");
                content.Add("");
            }

            // Build Order Pattern Section (from local replay cache)
            if (!string.IsNullOrEmpty(profile.CurrentBuildPattern.MostFrequentBuild))
            {
                content.Add("[bold yellow]Favorite Opening[/]");
                content.Add($"  Build: [cyan]{Escape(profile.CurrentBuildPattern.MostFrequentBuild)}[/]");
                content.Add($"  Last Observed: {profile.CurrentBuildPattern.LastAnalyzed:g}");
                content.Add("");
            }

            // SC2Pulse Live Stats Section - CLEARLY LABELED AS EXTERNAL DATA
            if (profile.LiveStats != null)
            {
                content.Add("[bold magenta]━━━━━ SC2PULSE LIVE DATA ━━━━━[/]");
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

                // Show race stats if available from SC2Pulse
                if (profile.LiveStats.RaceStats != null)
                {
                    var raceStats = profile.LiveStats.RaceStats;

                    content.Add("[bold yellow]Race Statistics[/]");
                    var terranDisplay = $"Terran: {raceStats.Terran.Wins}W-{raceStats.Terran.Losses}L";
                    var protossDisplay = $"Protoss: {raceStats.Protoss.Wins}W-{raceStats.Protoss.Losses}L";
                    var zergDisplay = $"Zerg: {raceStats.Zerg.Wins}W-{raceStats.Zerg.Losses}L";

                    content.Add($"  {terranDisplay} | {protossDisplay} | {zergDisplay}");
                    content.Add("");
                }
            }

            if (profile.RecentMatches.Count > 0)
            {
                content.Add("[bold magenta]━━━━━ SC2PULSE RECENT MATCHES ━━━━━[/]");
                foreach (var match in profile.RecentMatches)
                {
                    var result = match.OpponentWon ? "[green]WIN[/]" : "[red]LOSS[/]";
                    var duration = match.Duration.HasValue ? $" ({match.Duration.Value:mm\\:ss})" : string.Empty;
                    var opponentLabel = string.IsNullOrWhiteSpace(match.EnemyBattleTag)
                        ? Escape(match.EnemyName)
                        : Escape(match.EnemyBattleTag);
                    content.Add($"  {match.PlayedAt:g} - {Escape(match.MapName)} vs {opponentLabel} [{Escape(match.EnemyRace)}] {result}{duration}");
                }
                content.Add("");
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

        public string? PromptForNote(string yourTag, string opponentTag, string mapName)
        {
            AnsiConsole.MarkupLine($"[yellow]Leave note for:[/] [cyan]{Escape(yourTag)}[/] vs [magenta]{Escape(opponentTag)}[/] on [grey]{Escape(mapName)}[/]");
            AnsiConsole.Markup("[grey]Note (press Enter to skip): [/]");
            return System.Console.ReadLine();
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
                var toonInfo = string.IsNullOrEmpty(player.Toon)
                    ? string.Empty
                    : $", Toon: {Escape(player.Toon)}";
                AnsiConsole.MarkupLine($"  [cyan]-[/] {Escape(displayName)} [grey](Nick: {Escape(displayName)}, BattleTag: {Escape(playerTag)}{toonInfo})[/]");
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
