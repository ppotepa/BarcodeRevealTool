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

            // Run the engine
            var engine = serviceProvider.GetRequiredService<GameEngine>();
            await engine.Run();
        }
    }
}