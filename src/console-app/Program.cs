using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Extensions;
using BarcodeRevealTool.UI.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BarcodeRevealTool.ConsoleApp
{
    internal class Program
    {
        public static async Task Main(params string[] args)
        {
            // DEBUG MODE: Display discovered accounts
#if DEBUG
            DisplayDebugInfo();
            Console.WriteLine("\nPress ENTER to continue to normal operation...");
            Console.ReadLine();
#endif

            // Build configuration
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            // Setup dependency injection
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddBarcodeRevealEngine();
            services.AddSingleton<IOutputProvider, SpectreConsoleOutputProvider>();

            var serviceProvider = services.BuildServiceProvider();

            // Get engine and UI provider
            var engine = serviceProvider.GetRequiredService<GameEngine>();
            var outputProvider = serviceProvider.GetRequiredService<IOutputProvider>();

            // Wire event listeners
            // StateChanged: Full UI refresh on Awaiting ↔ InGame transitions
            engine.StateChanged += (sender, args) =>
            {
                // State change already triggers DisplayCurrentState in engine
            };

            // PeriodicStateUpdate: Called every 1500ms for animations/updates
            engine.PeriodicStateUpdate += (sender, args) =>
            {
                outputProvider.HandlePeriodicStateUpdate(args.CurrentState.ToString(), args.CurrentLobby);
            };

            // Run the game engine directly on the main thread
            try
            {
                await engine.Run();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Program] Error in game engine: {ex}");
                outputProvider.RenderError($"Engine error: {ex.Message}");
            }
        }

        /// <summary>
        /// Display debug information on startup: discovered user accounts and toon handles
        /// </summary>
        private static void DisplayDebugInfo()
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // Console may not be available in some environments (e.g., piped input)
            }
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║           BARCODE REVEAL TOOL - DEBUG STARTUP INFO             ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();

            // Display detected user account
            Console.WriteLine("📋 DETECTED USER ACCOUNT:");
            string? detectedUser = UserDetectionService.DetectUserAccount();
            if (!string.IsNullOrEmpty(detectedUser))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                // Extract nick name (everything before underscore)
                string nickName = detectedUser.Contains('_') ? detectedUser.Substring(0, detectedUser.IndexOf('_')) : detectedUser;
                Console.WriteLine($"  ✓ Nick: {nickName}");
                Console.WriteLine($"  ✓ Battle Tag: {detectedUser}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠ No user account detected from .lnk files");
                Console.ResetColor();
            }
            Console.WriteLine();

            // Display all discovered toon handles with nick name mapping
            Console.WriteLine("🎮 DISCOVERED TOON HANDLES:");
            var toonHandles = AccountToonDiscoveryService.DiscoverAllToonHandles();
            if (toonHandles.Count > 0)
            {
                var nickMapping = AccountToonDiscoveryService.DiscoverToonNickMapping();
                Console.ForegroundColor = ConsoleColor.Cyan;
                foreach (var toon in toonHandles)
                {
                    var region = AccountToonDiscoveryService.ExtractRegion(toon);
                    var realm = AccountToonDiscoveryService.ExtractRealm(toon);
                    var id = AccountToonDiscoveryService.ExtractBattleNetId(toon);

                    string regionName = region switch
                    {
                        "1" => "Americas",
                        "2" => "Europe",
                        "3" => "Asia",
                        "5" => "China",
                        "6" => "SEA",
                        _ => "Unknown"
                    };

                    // Include nick name if available from mapping
                    string nickInfo = "";
                    if (nickMapping.ContainsKey(toon))
                    {
                        var (nick, discriminator) = nickMapping[toon];
                        nickInfo = $" ({nick}#{discriminator})";
                    }

                    Console.WriteLine($"  • {toon}{nickInfo} ({regionName}, Realm {realm}, ID {id})");
                }
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠ No toon handles found in account folders");
                Console.ResetColor();
            }
            Console.WriteLine();

            // Display SC2 paths being scanned
            Console.WriteLine("📁 SC2 REPLAY FOLDER:");
            var appSettings = new AppSettings();
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
            config.GetSection("barcodeReveal").Bind(appSettings);

            if (!string.IsNullOrEmpty(appSettings.Replays?.Folder))
            {
                if (Directory.Exists(appSettings.Replays.Folder))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ✓ {appSettings.Replays.Folder}");
                    Console.ResetColor();

                    var replayCount = Directory.GetFiles(appSettings.Replays.Folder, "*.SC2Replay",
                        appSettings.Replays.Recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly).Length;
                    Console.WriteLine($"    Found {replayCount} replay files");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ Path not found: {appSettings.Replays.Folder}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ⚠ Replay folder not configured in appsettings.json");
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }
}