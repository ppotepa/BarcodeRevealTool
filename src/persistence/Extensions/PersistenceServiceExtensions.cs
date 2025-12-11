using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Persistence.Cache;
using BarcodeRevealTool.Persistence.Database;
using BarcodeRevealTool.Persistence.Replay;
using BarcodeRevealTool.Persistence.Repositories;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BarcodeRevealTool.Persistence.Extensions
{
    /// <summary>
    /// Extension methods for registering persistence services in the DI container.
    /// Configures all database access, repositories, and the unit of work pattern.
    /// </summary>
    public static class PersistenceServiceExtensions
    {
        /// <summary>
        /// Adds persistence services (database and cache) to the service collection.
        /// All database operations flow through the IUnitOfWork interface.
        /// </summary>
        public static IServiceCollection AddPersistence(this IServiceCollection services, string? customDatabasePath = null)
        {
            // Get or construct the connection string
            var connectionString = customDatabasePath != null
                ? $"Data Source={customDatabasePath};"
                : $"Data Source={Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "_db", "cache.db")};";

            // Register the Unit of Work - centralized access to all repositories
            services.AddSingleton<IUnitOfWork>(sp => new UnitOfWork(connectionString));

            // Register the replay database as a singleton
            services.AddSingleton(_ => new ReplayQueryService(customDatabasePath));

            // Register the cache database as a singleton
            services.AddSingleton(_ => new ReplayDatabase(customDatabasePath));

            // Register the database as implementations of the existing IReplayRepository (from Engine domain)
            // This is for backward compatibility with existing code
            services.AddSingleton<Engine.Domain.Abstractions.IReplayRepository>(sp => sp.GetRequiredService<ReplayDatabase>());
            services.AddSingleton<IReplayPersistence>(sp => sp.GetRequiredService<ReplayDatabase>());

            // Register replay cache service for scanning and caching replays
            services.AddSingleton(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var queryService = sp.GetRequiredService<ReplayQueryService>();
                return new ReplayCacheService(config, queryService);
            });

            // Register the cache manager as a singleton
            services.AddSingleton(sp =>
            {
                var db = sp.GetRequiredService<ReplayDatabase>();
                var replayCacheService = sp.GetRequiredService<ReplayCacheService>();
                return new CacheManager(db, replayCacheService);
            });

            // Register the cache manager as an implementation of ICacheManager
            services.AddSingleton<ICacheManager>(sp => sp.GetRequiredService<CacheManager>());

            // Register new data tracking services using the Unit of Work
            services.AddSingleton(sp =>
            {
                var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
                return new LobbyFileService(unitOfWork);
            });
            services.AddSingleton<ConfigInitializationService>();
            services.AddSingleton(sp =>
            {
                var lobbyFileService = sp.GetRequiredService<LobbyFileService>();
                var configService = sp.GetRequiredService<ConfigInitializationService>();
                var unitOfWork = sp.GetRequiredService<IUnitOfWork>();

                return new DataTrackingIntegrationService(lobbyFileService, configService, unitOfWork);
            });

            return services;
        }
    }
}
