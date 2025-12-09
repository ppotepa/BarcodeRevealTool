using Microsoft.Extensions.DependencyInjection;
using BarcodeRevealTool.Engine.Domain.Abstractions;
using BarcodeRevealTool.Persistence.Database;
using BarcodeRevealTool.Persistence.Cache;

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
            // Register the database as a singleton
            services.AddSingleton(_ => new ReplayDatabase(customDatabasePath));

            // Register the database as implementations of the repository and persistence interfaces
            services.AddSingleton<IReplayRepository>(sp => sp.GetRequiredService<ReplayDatabase>());
            services.AddSingleton<IReplayPersistence>(sp => sp.GetRequiredService<ReplayDatabase>());

            // Register the cache manager as a singleton
            services.AddSingleton(sp =>
            {
                var db = sp.GetRequiredService<ReplayDatabase>();
                return new CacheManager(db);
            });

            // Register the cache manager as an implementation of ICacheManager
            services.AddSingleton<ICacheManager>(sp => sp.GetRequiredService<CacheManager>());

            return services;
        }
    }
}
