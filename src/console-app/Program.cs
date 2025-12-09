using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Application;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Extensions;
using BarcodeRevealTool.Engine.Presentation;
using BarcodeRevealTool.Persistence.Extensions;
using BarcodeRevealTool.UI.Console;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

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

            ServiceProvider? provider = null;
            IErrorRenderer? errorRenderer = null;

            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                ConfigureLogging();
                Log.Information("Starting BarcodeRevealTool...");

                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(config);
                services.AddPersistence();
                services.AddBarcodeRevealEngine(config);
                services.AddSingleton<SpectreConsoleOutputProvider>();
                services.AddSingleton<IGameStateRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton<IMatchHistoryRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton<IBuildOrderRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton<IErrorRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());

                provider = services.BuildServiceProvider();
                
                // Initialize cache based on mode and lock file presence
                var cacheManager = provider.GetRequiredService<BarcodeRevealTool.Persistence.Cache.CacheManager>();
                await InitializeCacheAsync(cacheManager, config);
                
                var orchestrator = provider.GetRequiredService<GameOrchestrator>();
                errorRenderer = provider.GetRequiredService<IErrorRenderer>();

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
                errorRenderer?.RenderError(ex.Message);
                Log.Fatal(ex, "Application terminated unexpectedly");
            }
            finally
            {
                provider?.Dispose();
                Log.CloseAndFlush();
            }
        }

        private static async Task InitializeCacheAsync(BarcodeRevealTool.Persistence.Cache.CacheManager cacheManager, IConfiguration config)
        {
            string lockFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
            bool lockFileExists = File.Exists(lockFilePath);

#if DEBUG
            // In DEBUG mode, we already showed the startup menu
            // Option 1: Normal start - use cache (lock file exists from menu handling)
            // Option 2: Start Fresh - cache was deleted in menu, no lock file
            
            try
            {
                await cacheManager.InitializeAsync();
                Log.Information("Cache initialized in DEBUG mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize cache in DEBUG mode");
                throw;
            }
#else
            // In RELEASE mode, no startup menu - check lock file
            if (!lockFileExists)
            {
                // No lock file means first run or cache was cleared - sync replays from appconfig
                Log.Information("No cache lock file found. Syncing replays from configuration...");
                
                var appSettings = new AppSettings();
                config.GetSection("barcodeReveal").Bind(appSettings);
                
                if (!string.IsNullOrEmpty(appSettings.Replays?.Folder))
                {
                    try
                    {
                        await cacheManager.InitializeAsync();
                        await cacheManager.SyncFromDiskAsync(appSettings.Replays.Folder, appSettings.Replays.Recursive);
                        Log.Information("Replay synchronization completed");
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Failed to sync replays from disk");
                        throw;
                    }
                }
                else
                {
                    Log.Warning("No replay folder configured in appSettings");
                    await cacheManager.InitializeAsync();
                }
            }
            else
            {
                // Lock file exists - use existing cache
                try
                {
                    await cacheManager.InitializeAsync();
                    Log.Information("Cache initialized in RELEASE mode with existing data");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to initialize cache in RELEASE mode");
                    throw;
                }
            }
#endif
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
        private static void ConfigureLogging()
        {
            var runStamp = DateTime.UtcNow.ToString("HHmmssfff");
            var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");
#if DEBUG
            var flavor = "debug";
#else
            var flavor = Debugger.IsAttached ? "debug" : "nondebug";
#endif
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, $"{dateStamp}.{runStamp}.{flavor}.log");

            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("RunId", runStamp)
                .WriteTo.File(
                    logPath,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    rollingInterval: RollingInterval.Infinite,
                    retainedFileCountLimit: 10,
                    shared: true);

#if DEBUG
            loggerConfig = loggerConfig.MinimumLevel.Debug()
                .WriteTo.Debug();
#else
            loggerConfig = loggerConfig.MinimumLevel.Information();
            if (Debugger.IsAttached)
            {
                loggerConfig = loggerConfig.WriteTo.Debug();
            }
#endif

            Log.Logger = loggerConfig.CreateLogger();
        }
    }
}
