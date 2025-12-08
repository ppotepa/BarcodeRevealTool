using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
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
            // DEBUG MODE: Display discovered accounts and startup menu
#if DEBUG
            DisplayDebugInfo();
            ShowStartupMenu();
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
        /// Display startup menu in DEBUG mode with options for normal or fresh start
        /// </summary>
        private static void ShowStartupMenu()
        {
            Console.WriteLine();
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                    STARTUP OPTIONS                             ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
            Console.WriteLine();
            Console.WriteLine("  1. Start (normal operation with cached data)");
            Console.WriteLine("  2. Start Fresh (delete cache and restart)");
            Console.WriteLine();

            string choice = "";
            bool validChoice = false;

            while (!validChoice)
            {
                Console.Write("Select option (1 or 2): ");
                choice = Console.ReadLine() ?? "";

                if (choice == "1")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("✓ Starting with normal operation...");
                    Console.ResetColor();
                    validChoice = true;
                }
                else if (choice == "2")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("⚠ Starting fresh - deleting cache...");
                    Console.ResetColor();
                    DeleteCacheFiles();
                    validChoice = true;
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("✗ Invalid choice. Please enter 1 or 2.");
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }

            Console.WriteLine();
        }

        /// <summary>
        /// Delete cache files for a fresh start
        /// </summary>
        private static void DeleteCacheFiles()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db");
                string cacheDbPath = Path.Combine(dbPath, "cache.db");
                string cacheLockFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
                string cacheValidationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.validation");

                // Delete database and related files
                if (File.Exists(cacheDbPath))
                {
                    File.Delete(cacheDbPath);
                    System.Diagnostics.Debug.WriteLine($"[Program] Deleted cache database: {cacheDbPath}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"  Deleted: cache.db");
                    Console.ResetColor();
                }

                // Delete cache lock file
                if (File.Exists(cacheLockFile))
                {
                    File.Delete(cacheLockFile);
                    System.Diagnostics.Debug.WriteLine($"[Program] Deleted cache lock file: {cacheLockFile}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"  Deleted: cache.lock");
                    Console.ResetColor();
                }

                // Delete cache validation file
                if (File.Exists(cacheValidationFile))
                {
                    File.Delete(cacheValidationFile);
                    System.Diagnostics.Debug.WriteLine($"[Program] Deleted cache validation file: {cacheValidationFile}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"  Deleted: cache.validation");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✓ Cache cleaned successfully. Starting fresh...");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"✗ Error cleaning cache: {ex.Message}");
                Console.ResetColor();
                System.Diagnostics.Debug.WriteLine($"[Program] Error during cache cleanup: {ex}");
            }
        }

        /// <summary>
        /// <summary>
        /// <summary>
        /// Display debug information on startup about the configured environment.
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

            Console.WriteLine("ษออออออออออออออออออออออออออออออออออออออออออออออออออออออออออออออออป");
            Console.WriteLine("บ           BARCODE REVEAL TOOL - DEBUG STARTUP INFO             บ");
            Console.WriteLine("ศออออออออออออออออออออออออออออออออออออออออออออออออออออออออออออออออผ");
            Console.WriteLine();

            var appSettings = new AppSettings();
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
            config.GetSection("barcodeReveal").Bind(appSettings);

            Console.WriteLine("?? CONFIGURED USER ACCOUNT:");
            if (!string.IsNullOrWhiteSpace(appSettings.User?.BattleTag))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ๛ Battle Tag: {appSettings.User.BattleTag}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  ? Missing barcodeReveal:user:battleTag in appsettings.json");
                Console.ResetColor();
            }
            Console.WriteLine();

            Console.WriteLine("?? SC2 REPLAY FOLDER:");
            if (!string.IsNullOrEmpty(appSettings.Replays?.Folder))
            {
                if (Directory.Exists(appSettings.Replays.Folder))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ๛ Folder: {appSettings.Replays.Folder}");
                    Console.WriteLine($"  ๛ Recursive: {appSettings.Replays.Recursive}");
                    Console.WriteLine($"  ๛ Show Last Build Order: {appSettings.Replays.ShowLastBuildOrder}");
                    Console.ResetColor();
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ? Configured folder not found: {appSettings.Replays.Folder}");
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ? Replay folder not configured");
                Console.ResetColor();
            }
            Console.WriteLine();

            Console.WriteLine("?? CACHE STATUS:");
            string cacheDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db");
            if (File.Exists(cacheDbPath))
            {
                var info = new FileInfo(cacheDbPath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  ๛ Cache DB found ({info.Length / (1024 * 1024)} MB)");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  ? Cache DB not found (will be created)");
                Console.ResetColor();
            }
            Console.WriteLine();
        }
    }
}
