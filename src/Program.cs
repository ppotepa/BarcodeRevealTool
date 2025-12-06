using BarcodeRevealTool.Game;

namespace BarcodeRevealTool
{
    internal class Program
    {
        public static Task Main(params string[] args)
        {
            var first = File.ReadAllBytes(Path.Combine(Directory.GetCurrentDirectory(), "./lobbies/first.battlelobby"));
            var lobby = new GameLobby(first);

            Console.WriteLine(lobby.P1);
            Console.WriteLine(lobby.P2);

            return Task.CompletedTask;
        }
    }
}
