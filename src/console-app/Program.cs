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
    }
}