using BarcodeRevealTool.Game;
using Flurl.Http;

namespace BarcodeRevealTool
{
    internal class Program
    {
        public async static Task Main(params string[] args)
        {
            var dir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            while (true)
            {
                var lobbyFilePath = Path.Combine(dir, "Temp", "Starcraft II", "TempWriteReplayP1", "replay.server.battlelobby");

                if (File.Exists(lobbyFilePath))
                {
                    var lobbyBytes = File.ReadAllBytes(lobbyFilePath);
                    var lobby = new GameLobby(lobbyBytes);

                    Console.Clear();
                    Console.WriteLine(lobby.P1);
                    Console.WriteLine(lobby.P2);


                    //todo : fix obtaining data from external servicd

                    var profile = await "https://sc2pulse.nephest.com/sc2/api/characters?query=Originator%2321343".GetJsonAsync<dynamic>();
                    await Task.Delay(1000);
                }
                else
                {
                    Console.Clear();
                    Console.WriteLine("Awaiting for the game to start...");
                    await Task.Delay(500);
                }
            }
        }
    }
}
