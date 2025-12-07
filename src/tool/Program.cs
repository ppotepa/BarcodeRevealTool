using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sc2Pulse;


namespace BarcodeRevealTool
{
    internal class Program
    {
        public async static Task Main(params string[] args)
        {
            ServiceCollection collection = new ServiceCollection();
            RegisterServices(collection);

            var serviceProvider = collection.BuildServiceProvider();
            var tool = serviceProvider.GetService<RevealTool>();

            if (tool != null)
            {
                await tool.Run();
            }
        }

        private static void RegisterServices(IServiceCollection services)
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();

            services.AddSingleton<IConfiguration>(config);
            services.AddScoped<RevealTool>();
            services.AddTransient<Sc2PulseClient>();
        }
    }
}
