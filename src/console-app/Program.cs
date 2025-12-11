using BarcodeRevealTool.ConsoleApp.Debugging;
using BarcodeRevealTool.Engine.Application;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Extensions;
using BarcodeRevealTool.Engine.Presentation;
using BarcodeRevealTool.Persistence.Cache;
using BarcodeRevealTool.Persistence.Extensions;
using BarcodeRevealTool.Persistence.Replay;
using BarcodeRevealTool.Persistence.Schema.Migrations;
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

            try
            {
                var config = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .Build();

                // Initialize database connection and get run number before configuring logging
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db");
                var connectionString = $"Data Source={dbPath};";

                // Run migrations before any database operations
                try
                {
                    Directory.CreateDirectory(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db"));
                    var migrationRunner = new MigrationRunner(connectionString);
                    var migrationResult = await migrationRunner.RunAllMigrationsAsync();
                    if (!migrationResult.Success)
                    {
                        Log.Warning("Some migrations failed: {@MigrationDetails}", migrationResult);
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Migration setup encountered an issue, continuing anyway");
                }

                // Get run number before Serilog is configured
                RunInfoService? tempRunInfoService = null;
                try
                {
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

                var debugSettings = new DebugSettings();
#if DEBUG
                // Show startup menu after logging is configured so choices get logged
                debugSettings = ShowStartupMenu(config) ?? new DebugSettings();
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
                services.AddSingleton<IMatchNotePrompt>(sp => sp.GetRequiredService<SpectreConsoleOutputProvider>());
                services.AddSingleton(tempRunInfoService ?? new RunInfoService(connectionString, Log.Logger));

                provider = services.BuildServiceProvider();

                // Initialize config history snapshot on startup
                var configInitService = provider.GetRequiredService<ConfigInitializationService>();
                configInitService.InitializeConfigHistoryOnStartup(runNumber);

                // Initialize data tracking for this run
                var dataTrackingService = provider.GetRequiredService<DataTrackingIntegrationService>();
                dataTrackingService.InitializeDebugSession(runNumber);

                // Update AppSettings in the provider with debug settings if set
                var appSettings = provider.GetRequiredService<AppSettings>();
                if (debugSettings.ManualBattleTag != null || debugSettings.ManualNickname != null || debugSettings.LobbyFiles?.Count > 0)
                {
                    appSettings.Debug = debugSettings;
                }

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

                // Mark data tracking session as completed
                var dataTrackingSvc = provider.GetRequiredService<DataTrackingIntegrationService>();
                await dataTrackingSvc.CompleteDebugSessionAsync(runNumber, 0);
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

                        // Also mark data tracking session as failed
                        if (provider != null)
                        {
                            var dataTrackingSvc = provider.GetRequiredService<DataTrackingIntegrationService>();
                            await dataTrackingSvc.CompleteDebugSessionAsync(runNumber, 1);
                        }
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
            var appSettings = new AppSettings();
            config.GetSection("barcodeReveal").Bind(appSettings);

            string lockFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache.lock");
            bool lockFileExists = await Task.Run(() => File.Exists(lockFilePath));

            try
            {
                await cacheManager.InitializeAsync();

                // Always ensure system tables exist (RunInfo, Players, ReplayFiles, DebugSession)
                // even if cache.lock exists - these are critical for operation
                await RepairSystemTablesAsync();

                // If cache.lock exists, the cache was previously initialized and is valid
                // Trust the cached data and don't re-extract
                if (lockFileExists)
                {
                    var stats = cacheManager.GetStatistics();
                    Log.Information("Cache lock file exists. Using existing cache with {Matches} matches", stats.TotalMatches);
                    return;
                }

                // No lock file means first run - check if database is empty
                bool cacheIsEmpty = await Task.Run(() => cacheManager.IsCacheEmpty());

                if (cacheIsEmpty && !string.IsNullOrEmpty(appSettings.Replays?.Folder))
                {
                    Log.Information("No cache lock file found and database is empty. Performing full replay extraction from disk...");
                    await cacheManager.SyncFromDiskAsync(appSettings.Replays.Folder, appSettings.Replays.Recursive == true);
                    Log.Information("Full cache synchronization completed");
                }
                else if (!cacheIsEmpty)
                {
                    var stats = cacheManager.GetStatistics();
                    Log.Information("Cache already populated with {Matches} matches", stats.TotalMatches);
                }
                else
                {
                    Log.Warning("No replay folder configured in appSettings");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize cache");
                throw;
            }
        }

        private static async Task RepairSystemTablesAsync()
        {
            try
            {
                var dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db");
                var connectionString = $"Data Source={dbPath};";

                await Task.Run(() =>
                {
                    using var connection = new System.Data.SQLite.SQLiteConnection(connectionString);
                    connection.Open();

                    // Check which system tables exist
                    var missingTables = new List<string>();
                    var systemTables = new[] { "RunInfo", "Players", "ReplayFiles", "DebugSession", "UserConfig" };

                    foreach (var tableName in systemTables)
                    {
                        if (!TableExists(connection, tableName))
                        {
                            missingTables.Add(tableName);
                        }
                    }

                    if (missingTables.Count > 0)
                    {
                        Log.Warning("Found missing system tables: {Tables}. Recreating...", string.Join(", ", missingTables));

                        // Recreate missing tables
                        var tableSchemaMap = new Dictionary<string, string>
                        {
                            { "RunInfo", "RunInfo.sql" },
                            { "Players", "Players.sql" },
                            { "ReplayFiles", "ReplayFiles.sql" },
                            { "DebugSession", "Debug.sql" },
                            { "UserConfig", "UserConfig.sql" }
                        };

                        foreach (var table in missingTables)
                        {
                            if (tableSchemaMap.TryGetValue(table, out var schemaFile))
                            {
                                try
                                {
                                    BarcodeRevealTool.Persistence.Schema.SchemaLoader.ExecuteSchema(connection, schemaFile);
                                    Log.Information("Recreated table: {Table}", table);
                                }
                                catch (Exception ex)
                                {
                                    Log.Error(ex, "Failed to recreate table {Table}", table);
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Information("All system tables exist and are valid");
                    }
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to repair system tables");
                throw;
            }
        }

        private static bool TableExists(System.Data.SQLite.SQLiteConnection connection, string tableName)
        {
            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = $"SELECT 1 FROM sqlite_master WHERE type='table' AND name=@tableName LIMIT 1";
                command.Parameters.Add(new System.Data.SQLite.SQLiteParameter("@tableName", tableName));
                return command.ExecuteScalar() != null;
            }
            catch
            {
                return false;
            }
        }

        private static DebugSettings? ShowStartupMenu(IConfiguration config)
        {
            Console.WriteLine();
            Console.WriteLine("===============================================================");
            Console.WriteLine("                     STARTUP OPTIONS                           ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();
            Console.WriteLine("  1. Start (normal operation with cached data)");
            Console.WriteLine("  2. Start Fresh (delete cache and restart)");
            Console.WriteLine("  3. SC2Pulse API Debugger (tools only)");
            Console.WriteLine("  4. Debug Mode (test with manual players or debug lobbies)");
            Console.WriteLine();

            string choice = string.Empty;
            while (true)
            {
                Console.Write("Select option (1-4): ");
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

                if (choice == "3")
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("Launching SC2Pulse API debugger...");
                    Console.ResetColor();
                    Log.Information("User selected: SC2Pulse Debugger (option 3)");
                    Sc2PulseDebugMenu.RunAsync().GetAwaiter().GetResult();
                    Console.WriteLine();
                    continue;
                }

                if (choice == "4")
                {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("Entering Debug Mode...");
                    Console.ResetColor();
                    Log.Information("User selected: Debug Mode (option 4)");
                    return ShowDebugMenu(config);
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid choice.");
                Console.ResetColor();
                Log.Warning("Invalid menu choice entered: {Choice}", choice);
            }

            Console.WriteLine();
            return null;
        }

        private static DebugSettings ShowDebugMenu(IConfiguration config)
        {
            Console.WriteLine();
            Console.WriteLine("===============================================================");
            Console.WriteLine("                     DEBUG MODE OPTIONS                        ");
            Console.WriteLine("===============================================================");
            Console.WriteLine();
            Console.WriteLine("  1. Use lobby files from debug/lobbies/ folder");
            Console.WriteLine("  2. Enter opponent details manually");
            Console.WriteLine();

            var appSettings = new AppSettings();
            config.GetSection("barcodeReveal").Bind(appSettings);
            var debugSettings = new DebugSettings();

            string choice = string.Empty;
            while (true)
            {
                Console.Write("Select option (1-2): ");
                choice = Console.ReadLine() ?? string.Empty;

                if (choice == "1")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Using debug lobby files...");
                    Console.ResetColor();
                    Log.Information("User selected: Debug lobby folder mode");

                    // Load lobby files from folder
                    string debugFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug", "lobbies");
                    if (Directory.Exists(debugFolder))
                    {
                        var lobbyFiles = Directory.GetFiles(debugFolder, "*.sc2replay", SearchOption.TopDirectoryOnly)
                            .OrderBy(f => f)
                            .ToList();

                        if (lobbyFiles.Count > 0)
                        {
                            debugSettings.LobbyFiles = lobbyFiles;
                            Console.WriteLine($"  Found {lobbyFiles.Count} lobby file(s) in {debugFolder}");
                            foreach (var file in lobbyFiles)
                            {
                                Console.WriteLine($"    - {Path.GetFileName(file)}");
                            }
                            Log.Information("Debug mode: Loaded {Count} lobby files from folder: {Folder}", lobbyFiles.Count, debugFolder);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"No .sc2replay files found in {debugFolder}");
                            Console.ResetColor();
                            Log.Warning("Debug mode: No lobby files found in folder: {Folder}", debugFolder);
                        }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Folder not found: {debugFolder}");
                        Console.ResetColor();
                        Log.Error("Debug mode: Lobby folder not found: {Folder}", debugFolder);
                    }
                    break;
                }

                if (choice == "2")
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Manual opponent entry mode...");
                    Console.ResetColor();
                    Log.Information("User selected: Manual opponent entry mode");

                    Console.WriteLine();
                    Console.Write("Enter opponent battle tag (e.g., Opponent#1234): ");
                    string battleTag = Console.ReadLine() ?? string.Empty;

                    Console.Write("Enter opponent nickname: ");
                    string nickname = Console.ReadLine() ?? string.Empty;

                    if (!string.IsNullOrEmpty(battleTag) && !string.IsNullOrEmpty(nickname))
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Debug mode: Using opponent {nickname} ({battleTag})");
                        Console.ResetColor();

                        // Store in debug settings to return
                        debugSettings.ManualBattleTag = battleTag;
                        debugSettings.ManualNickname = nickname;

                        Log.Information("Debug mode: Manual opponent set - {Nickname} ({BattleTag})", nickname, battleTag);
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Invalid input. Please try again.");
                        Console.ResetColor();
                        Log.Warning("Debug mode: Invalid opponent details provided");
                        continue;
                    }
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Invalid choice.");
                Console.ResetColor();
                Log.Warning("Invalid debug menu choice entered: {Choice}", choice);
            }

            Console.WriteLine();
            return debugSettings;
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
