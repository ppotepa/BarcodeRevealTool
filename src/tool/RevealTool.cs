using BarcodeRevealTool.Game;
using Microsoft.Extensions.Configuration;

namespace BarcodeRevealTool
{
    internal class RevealTool
    {

        private bool _running = true;

        public RevealTool(IConfiguration configuration, IServiceProvider services)
        {
            configuration.Bind(Configuration);
            this.Services = services;
        }

        public string AppDataLocal
            => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        public string LobbyFilePath
            => Path.Combine(AppDataLocal, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");

        public ToolConfiguration? Configuration { get; private set; }
        public IServiceProvider Services { get; }

        public async Task Run()
        {
            while (_running)
            {
                if (File.Exists(LobbyFilePath))
                {
                    var lobbyBytes = File.ReadAllBytes(LobbyFilePath);

                    var factory = Services.GetService(typeof(GameLobbyFactory)) as GameLobbyFactory;
                    var lobby = factory?.CreateLobby(lobbyBytes);


                    if (lobby is not null)
                    {
                        lobby.PrintLobbyInfo(Console.Out);
                        lobby.PrintAdditionalPlayerData();
                    }
                    else
                    {
                        throw new InvalidOperationException("Failed to parse lobby data.");
                    }

                    //todo : fix obtaining data from external servicd
                    await Task.Delay(Configuration?.RefreshInterval ?? 100);
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
