using BarcodeRevealTool.Adapters;
using BarcodeRevealTool.Engine;
using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Replay;
using BarcodeRevealTool.Game;
using BarcodeRevealTool.Game.Lobbies.Strategies;
using BarcodeRevealTool.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sc2Pulse;

namespace BarcodeRevealTool.Engine.Extensions
{
    /// <summary>
    /// Extension methods for registering BarcodeRevealTool engine services
    /// </summary>
    public static class ServiceExtensions
    {
        /// <summary>
        /// Add all BarcodeRevealTool engine services to the dependency injection container
        /// </summary>
        public static IServiceCollection AddBarcodeRevealEngine(this IServiceCollection services)
        {
            // Register AppSettings from configuration
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var appSettings = new AppSettings();
                config.GetSection("barcodeReveal").Bind(appSettings);
                return appSettings;
            });

            // User identification strategy
            services.AddSingleton<IUserIdentificationStrategy>(sp =>
                new ConfigBasedUserStrategy(sp.GetRequiredService<AppSettings>()));

            // Core engine
            services.AddScoped<GameEngine>();

            // Game logic
            services.AddScoped<GameLobbyFactory>();

            // Database and queries
            services.AddScoped<IReplayQueryService, ReplayQueryService>();
            services.AddScoped<IBuildOrderCacheManager, BuildOrderCacheManager>();
            services.AddScoped<IReplayCacheService, ReplayCacheService>();

            // Services
            services.AddScoped<IReplayService, ReplayService>();
            services.AddScoped<IGameLobbyFactory, GameLobbyFactoryAdapter>();

            // External clients
            services.AddTransient<Sc2PulseClient>();

            return services;
        }
    }
}
