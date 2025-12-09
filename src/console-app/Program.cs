using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Application;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Extensions;
using BarcodeRevealTool.Engine.Presentation;
using BarcodeRevealTool.UI.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BarcodeRevealTool.ConsoleApp
{
    internal class Program
    {
        public static async Task Main(params string[] args)
        {
#if DEBUG
            DisplayDebugInfo();
            ShowStartupMenu();
#endif

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddBarcodeRevealEngine(config);
            services.AddSingleton<SpectreConsoleOutputProvider>();
            services.AddSingleton<IGameStateRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
            services.AddSingleton<IMatchHistoryRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
            services.AddSingleton<IBuildOrderRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
            services.AddSingleton<IErrorRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());

            var provider = services.BuildServiceProvider();
            var orchestrator = provider.GetRequiredService<GameOrchestrator>();
            var errorRenderer = provider.GetRequiredService<IErrorRenderer>();

            try
            {
                using var cts = new CancellationTokenSource();
                System.Console.CancelKeyPress += (sender, eventArgs) =>
                {
                    eventArgs.Cancel = true;
                    cts.Cancel();
                };

                await orchestrator.RunAsync(cts.Token);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Program] Orchestrator error: {ex}");
                errorRenderer.RenderError(ex.Message);
            }
        }

        private static void ShowStartupMenu()
        {
            Console.WriteLine();
            Console.WriteLine("===============================================================");
            Console.WriteLine("                     STARTUP OPTIONS                           ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();
            Console.WriteLine("  1. Start (normal operation with cached data)");
            Console.WriteLine("  2. Start Fresh (delete cache and restart)");
            Console.WriteLine();

            while (true)
            {
                Console.Write("Select option (1 or 2): ");
                var choice = Console.ReadLine() ?? string.Empty;

                if (choice == "1")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Starting normally...");
                    Console.ResetColor();
                    break;
                }

                if (choice == "2")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Deleting cache...");
                    Console.ResetColor();
                    DeleteCacheFiles();
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid choice.");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        private static void DeleteCacheFiles()
        {
            try
            {
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db");
                string cacheDbPath = Path.Combine(dbPath, "cache.db");
                string cacheLockFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
                string cacheValidationFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.validation");

                if (File.Exists(cacheDbPath))
                {
                    File.Delete(cacheDbPath);
                    Console.WriteLine("  Deleted: cache.db");
                }

                if (File.Exists(cacheLockFile))
                {
                    File.Delete(cacheLockFile);
                    Console.WriteLine("  Deleted: cache.lock");
                }

                if (File.Exists(cacheValidationFile))
                {
                    File.Delete(cacheValidationFile);
                    Console.WriteLine("  Deleted: cache.validation");
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cache cleaned successfully.");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error cleaning cache: {ex.Message}");
                Console.ResetColor();
            }
        }

        private static void DisplayDebugInfo()
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // ignore when console not available
            }

            Console.WriteLine("===============================================================");
            Console.WriteLine("             BARCODE REVEAL TOOL - DEBUG STARTUP INFO          ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();

            var appSettings = new AppSettings();
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
            config.GetSection("barcodeReveal").Bind(appSettings);

            Console.WriteLine("Configured Account:");
            if (!string.IsNullOrWhiteSpace(appSettings.User?.BattleTag))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Battle Tag: {appSettings.User.BattleTag}");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Missing barcodeReveal:user:battleTag");
            }
            Console.ResetColor();
            Console.WriteLine();

            Console.WriteLine("Replay Folder:");
            if (!string.IsNullOrEmpty(appSettings.Replays?.Folder))
            {
                var exists = Directory.Exists(appSettings.Replays.Folder);
                Console.ForegroundColor = exists ? ConsoleColor.Green : ConsoleColor.Yellow;
                Console.WriteLine($"  Folder: {appSettings.Replays.Folder}");
                Console.WriteLine($"  Recursive: {appSettings.Replays.Recursive}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Replay folder not configured.");
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Cache Status:");
            string cacheDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db");
            if (File.Exists(cacheDbPath))
            {
                var info = new FileInfo(cacheDbPath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Cache DB found ({info.Length / (1024 * 1024)} MB)");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Cache DB not found (will be created)");
            }
            Console.ResetColor();
            Console.WriteLine();
        }
    }
}
