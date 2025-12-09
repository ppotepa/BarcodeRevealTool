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
using BarcodeRevealTool.Engine.Presentation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

            services.AddSingleton<IReplayRepository, ReplayDataAccess>();
            services.AddSingleton<IReplayPersistence>(sp => (ReplayDataAccess)sp.GetRequiredService<IReplayRepository>());

            services.AddSingleton<IMatchHistoryService, MatchHistoryService>();
            services.AddSingleton<IBuildOrderService, BuildOrderService>();
            services.AddSingleton<IOpponentProfileService, OpponentProfileService>();

            services.AddSingleton<IReplaySyncService, ReplaySyncService>();
            services.AddSingleton<GameOrchestrator>();

            return services;
        }
    }
}
