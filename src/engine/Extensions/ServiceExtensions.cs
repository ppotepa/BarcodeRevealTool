using BarcodeRevealTool.Engine.Abstractions;
using BarcodeRevealTool.Engine.Application;
using BarcodeRevealTool.Engine.Application.Abstractions;
using BarcodeRevealTool.Engine.Application.Lobbies;
using BarcodeRevealTool.Engine.Application.Monitoring;
using BarcodeRevealTool.Engine.Application.Services;
using BarcodeRevealTool.Engine.Config;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Engine.Domain.Services;
using BarcodeRevealTool.Engine.Game;
using BarcodeRevealTool.Engine.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BarcodeRevealTool.Engine.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddBarcodeRevealEngine(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(sp =>
            {
                var settings = new AppSettings();
                configuration.GetSection("barcodeReveal").Bind(settings);
                return settings;
            });

            services.AddSingleton<IGameStateMonitor, GameStateMonitor>();
            services.AddSingleton<IGameLobbyFactory, GameLobbyFactory>();
            services.AddSingleton<ILobbyProcessor, LobbyProcessor>();

            services.TryAddSingleton<IReplayRepository, ReplayDataAccess>();
            services.TryAddSingleton<IReplayPersistence>(sp =>
            {
                var repository = sp.GetRequiredService<IReplayRepository>();
                if (repository is IReplayPersistence persistence)
                {
                    return persistence;
                }

                throw new InvalidOperationException("The registered IReplayRepository does not implement IReplayPersistence.");
            });

            services.AddSingleton<IMatchHistoryService, MatchHistoryService>();
            services.AddSingleton<IBuildOrderService, BuildOrderService>();
            services.AddSingleton<ISc2PulsePlayerStatsService, Sc2PulsePlayerStatsService>();
            services.AddSingleton<IOpponentProfileService, OpponentProfileService>();

            services.AddSingleton<IReplaySyncService, ReplaySyncService>();
            services.AddSingleton<GameOrchestrator>();

            return services;
        }
    }
}
