using BarcodeRevealTool.Game;

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
                //C:\Users\pawel\AppData\Local\Temp\StarCraft II\TempWriteReplayP1
                if (File.Exists(lobbyFilePath))
                {
                    var lobbyBytes = File.ReadAllBytes(lobbyFilePath);
                    var lobby = new GameLobby(lobbyBytes);

                    Console.Clear();
                    Console.WriteLine(lobby.P1);
                    Console.WriteLine(lobby.P2);
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
