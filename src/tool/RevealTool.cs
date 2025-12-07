using BarcodeRevealTool.Game;
using Microsoft.Extensions.Configuration;
using Sc2Pulse;

namespace BarcodeRevealTool
{
    internal class RevealTool
    {
        private bool _running = true;

        public RevealTool(IConfiguration configuration, IServiceProvider services, Sc2PulseClient pulseClient)
        {
            configuration.Bind(Configuration);
            Services = services;
            PulseClient = pulseClient;
        }

        public string AppDataLocal
            => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public string LobbyFilePath
            => Path.Combine(AppDataLocal, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");

        public AppSettings? Configuration { get; private set; } = new();
        public IServiceProvider Services { get; }
        public Sc2PulseClient PulseClient { get; }

        public async Task Run()
        {
            while (_running)
            {
                if (File.Exists(LobbyFilePath))
                {
                    var lobbyBytes = File.ReadAllBytes(LobbyFilePath);

                    var factory = Services.GetService(typeof(GameLobbyFactory)) as GameLobbyFactory;
                    var lobby = factory?.CreateLobby(lobbyBytes, Configuration);

                    if (lobby is not null)
                    {
                        lobby.PrintLobbyInfo(Console.Out);
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to parse lobby data.");
                    }

                    //todo : fix obtaining data from external servicd
                    await Task.Delay(Configuration?.RefreshInterval ?? 2000);
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("Awaiting for the game to start...");
                    await Task.Delay(500);
                }

                Console.Clear();
            }
        }
    }
}