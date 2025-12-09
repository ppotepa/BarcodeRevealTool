using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BarcodeRevealTool.Engine.Application;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Extensions;
using BarcodeRevealTool.Engine.Presentation;
using BarcodeRevealTool.Persistence.Cache;
using BarcodeRevealTool.Persistence.Extensions;
using BarcodeRevealTool.Persistence.Replay;
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
#endif

            ServiceProvider? provider = null;
            IErrorRenderer? errorRenderer = null;
            int runNumber = 0;
            string? userMenuChoice = null;

            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                // Initialize database connection and get run number before configuring logging
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db");
                var connectionString = $"Data Source={dbPath};";

                // Get run number before Serilog is configured
                RunInfoService? tempRunInfoService = null;
                try
                {
                    Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db"));
                    tempRunInfoService = new RunInfoService(connectionString, Log.Logger);

#if DEBUG
                    runNumber = tempRunInfoService.GetNextRunNumber("Debug");
#else
                    runNumber = tempRunInfoService.GetNextRunNumber(Debugger.IsAttached ? "Debug" : "Release");
#endif
                }
                catch
                {
                    // If we can't get a run number, default to 1
                    runNumber = 1;
                }

                ConfigureLogging(runNumber);
                Log.Information("Run Number: {RunNumber}", runNumber);

#if DEBUG
                // Show startup menu after logging is configured so choices get logged
                userMenuChoice = ShowStartupMenu();
#endif

                var services = new ServiceCollection();
                services.AddSingleton<IConfiguration>(config);
                services.AddPersistence();
                services.AddBarcodeRevealEngine(config);
                services.AddSingleton<SpectreConsoleOutputProvider>();
                services.AddSingleton<IGameStateRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton<IMatchHistoryRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton<IBuildOrderRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton<IErrorRenderer>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton(tempRunInfoService ?? new RunInfoService(connectionString, Log.Logger));

                provider = services.BuildServiceProvider();
                AttachCacheProgressRendering(provider);

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

                // Mark run as completed successfully
                var runInfoSvc = provider.GetRequiredService<RunInfoService>();
                runInfoSvc.CompleteRun(runNumber, 0);
            }
            catch (Exception ex)
            {
                errorRenderer?.RenderError(ex.Message);
                Log.Fatal(ex, "Application terminated unexpectedly");

                // Mark run as failed if we have a run number
                if (runNumber > 0)
                {
                    try
                    {
                        var connectionString = $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db")};";
                        var runInfoService = new RunInfoService(connectionString, Log.Logger);
                        runInfoService.FailRun(runNumber, ex.Message);
                    }
                    catch { }
                }
            }
            finally
            {
                provider?.Dispose();
                Log.CloseAndFlush();
            }
        }

        private static readonly object CacheProgressLock = new();
        private static int _lastProgressLength = 0;

        private static void AttachCacheProgressRendering(ServiceProvider provider)
        {
            try
            {
                var replayCacheService = provider.GetService<ReplayCacheService>();
                if (replayCacheService != null)
                {
                    replayCacheService.OnProgress += RenderCacheProgress;
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to attach cache progress renderer");
            }
        }

        private static void RenderCacheProgress(string phase, int current, int total, string? message)
        {
            if (total <= 0)
            {
                return;
            }

            var percentage = (current / (double)total) * 100;
            var info = message ?? string.Empty;
            var text = $"[{phase}] {percentage,6:F1}% ({current}/{total}) {info}";

            lock (CacheProgressLock)
            {
                try
                {
                    if (System.Console.IsOutputRedirected)
                    {
                        System.Console.WriteLine(text);
                    }
                    else
                    {
                        var padded = text.PadRight(Math.Max(text.Length, _lastProgressLength));
                        System.Console.Write($"\r{padded}");
                        _lastProgressLength = padded.Length;
                        if (current >= total)
                        {
                            System.Console.WriteLine();
                            _lastProgressLength = 0;
                        }
                    }
                }
                catch
                {
                    // ignore console rendering errors (headless environments)
                }
            }
        }

        private static async Task InitializeCacheAsync(BarcodeRevealTool.Persistence.Cache.CacheManager cacheManager, IConfiguration config)
        {
            string lockFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
            bool lockFileExists = File.Exists(lockFilePath);
            var appSettings = new AppSettings();
            config.GetSection("barcodeReveal").Bind(appSettings);

#if DEBUG
            // In DEBUG mode, we showed the startup menu
            // Option 1: Normal start - use cache (lock file exists from menu handling)
            // Option 2: Start Fresh - cache was deleted in menu, no lock file

            try
            {
                await cacheManager.InitializeAsync();

                // If no lock file exists (fresh start), scan and cache replays
                if (!lockFileExists && !string.IsNullOrEmpty(appSettings.Replays?.Folder))
                {
                    Log.Information("Fresh start detected in DEBUG mode. Caching replays from configured folder...");
                    await cacheManager.SyncFromDiskAsync(appSettings.Replays.Folder, appSettings.Replays.Recursive);
                }

                Log.Information("Cache initialized in DEBUG mode");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize cache in DEBUG mode");
                throw;
            }
#else
            // In RELEASE mode, no startup menu - check lock file
            try
            {
                await cacheManager.InitializeAsync();
                
                if (!lockFileExists)
                {
                    // No lock file means first run or cache was cleared - sync replays from appconfig
                    Log.Information("No cache lock file found. Syncing replays from configuration...");
                    
                    if (!string.IsNullOrEmpty(appSettings.Replays?.Folder))
                    {
                        await cacheManager.SyncFromDiskAsync(appSettings.Replays.Folder, appSettings.Replays.Recursive);
                        Log.Information("Replay synchronization completed");
                    }
                    else
                    {
                        Log.Warning("No replay folder configured in appSettings");
                    }
                }
                else
                {
                    // Lock file exists - use existing cache
                    Log.Information("Cache initialized in RELEASE mode with existing data");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize cache in RELEASE mode");
                throw;
            }
#endif
        }

        private static string ShowStartupMenu()
        {
            Console.WriteLine();
            Console.WriteLine("===============================================================");
            Console.WriteLine("                     STARTUP OPTIONS                           ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();
            Console.WriteLine("  1. Start (normal operation with cached data)");
            Console.WriteLine("  2. Start Fresh (delete cache and restart)");
            Console.WriteLine();

            string choice = string.Empty;
            while (true)
            {
                Console.Write("Select option (1 or 2): ");
                choice = Console.ReadLine() ?? string.Empty;

                if (choice == "1")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Starting normally...");
                    Console.ResetColor();
                    Log.Information("User selected: Start (option 1) - Using cached data");
                    break;
                }

                if (choice == "2")
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Deleting cache...");
                    Console.ResetColor();
                    Log.Information("User selected: Start Fresh (option 2) - Clearing cache");
                    DeleteCacheFiles();
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid choice.");
                Console.ResetColor();
                Log.Warning("Invalid menu choice entered: {Choice}", choice);
            }

            Console.WriteLine();
            return choice;
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
                    Log.Information("Deleted cache database file: {FilePath}", cacheDbPath);
                }

                if (File.Exists(cacheLockFile))
                {
                    File.Delete(cacheLockFile);
                    Console.WriteLine("  Deleted: cache.lock");
                    Log.Information("Deleted cache lock file: {FilePath}", cacheLockFile);
                }

                if (File.Exists(cacheValidationFile))
                {
                    File.Delete(cacheValidationFile);
                    Console.WriteLine("  Deleted: cache.validation");
                    Log.Information("Deleted cache validation file: {FilePath}", cacheValidationFile);
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Cache cleaned successfully.");
                Console.ResetColor();
                Log.Information("Cache cleanup completed successfully");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error cleaning cache: {ex.Message}");
                Console.ResetColor();
                Log.Error(ex, "Error while cleaning cache");
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
                Log.Debug("Debug Info - Configured Battle Tag: {BattleTag}", appSettings.User.BattleTag);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Missing barcodeReveal:user:battleTag");
                Log.Warning("Debug Info - Missing Battle Tag configuration");
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
                Log.Debug("Debug Info - Replay Folder: {Folder}, Recursive: {Recursive}, Exists: {Exists}",
                    appSettings.Replays.Folder, appSettings.Replays.Recursive, exists);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Replay folder not configured.");
                Console.ResetColor();
                Log.Warning("Debug Info - Replay folder not configured");
            }

            Console.WriteLine();
            Console.WriteLine("Cache Status:");
            string cacheDbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db");
            if (File.Exists(cacheDbPath))
            {
                var info = new FileInfo(cacheDbPath);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  Cache DB found ({info.Length / (1024 * 1024)} MB)");
                Log.Debug("Debug Info - Cache database exists: {Size} MB", info.Length / (1024 * 1024));
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("  Cache DB not found (will be created)");
                Log.Debug("Debug Info - Cache database does not exist (will be created)");
            }
            Console.ResetColor();
            Console.WriteLine();
        }
        private static void ConfigureLogging(int runNumber)
        {
            var dateStamp = DateTime.UtcNow.ToString("yyyyMMdd");
#if DEBUG
            var flavor = "debug";
#else
            var flavor = Debugger.IsAttached ? "debug" : "release";
#endif
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);
            var logPath = Path.Combine(logDirectory, $"{dateStamp}_{runNumber:D4}_{flavor}.log");

            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .Enrich.WithProperty("RunNumber", runNumber)
                .Enrich.WithProperty("Flavor", flavor)
                .WriteTo.File(
                    logPath,
                    restrictedToMinimumLevel: LogEventLevel.Debug,
                    outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{Level:u3}] [{RunNumber:D4}] {Message:lj}{NewLine}{Exception}",
                    rollingInterval: RollingInterval.Infinite,
                    retainedFileCountLimit: 30,
                    shared: true);

#if DEBUG
            loggerConfig = loggerConfig.MinimumLevel.Debug()
                .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
#else
            loggerConfig = loggerConfig.MinimumLevel.Information();
            if (Debugger.IsAttached)
            {
                loggerConfig = loggerConfig.WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");
            }
#endif

            Log.Logger = loggerConfig.CreateLogger();
            Log.Information("═══════════════════════════════════════════════════════════════");
            Log.Information("BarcodeRevealTool Session Started - Run #{RunNumber}", runNumber);
            Log.Information("═══════════════════════════════════════════════════════════════");
        }
    }
}
