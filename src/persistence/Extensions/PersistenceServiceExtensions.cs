using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Persistence.Cache;
using BarcodeRevealTool.Persistence.Database;
using BarcodeRevealTool.Persistence.Replay;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BarcodeRevealTool.Persistence.Extensions
{
    /// <summary>
    /// Extension methods for registering persistence services in the DI container.
    /// </summary>
    public static class PersistenceServiceExtensions
    {
        /// <summary>
        /// Adds persistence services (database and cache) to the service collection.
        /// </summary>
        public static IServiceCollection AddPersistence(this IServiceCollection services, string? customDatabasePath = null)
        {
            // Register the replay database as a singleton
            services.AddSingleton(_ => new ReplayQueryService(customDatabasePath));

            // Register the cache database as a singleton
            services.AddSingleton(_ => new ReplayDatabase(customDatabasePath));

            // Register the database as implementations of the repository and persistence interfaces
            services.AddSingleton<IReplayRepository>(sp => sp.GetRequiredService<ReplayDatabase>());
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

            return services;
        }
    }
}
