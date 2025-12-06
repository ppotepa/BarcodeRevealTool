using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Game
{

    /// <summary>
    /// as of now we wanna just support 1v1 mode, prolly the structure is a bit different for team games, also arcase, 
    /// would need dig deeper into the lobby file structure
    /// </summary>
    internal class GameLobby
    {
        private readonly byte[]? bytes;
        private readonly MatchCollection players;
        public Regex Pattern = new Regex("(?<name>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");

        private Player? _p2;
        private Player? _p1;

        public GameLobby(byte[] bytes)
        {

            this.bytes = bytes;
            this.players = Pattern.Matches(new string(bytes.Select(x => (char)x).ToArray()));

            if (players.Count % 2 == 0)
            {
                if (players.Count / 3 is 2)
                {

                    this.P1 = new Player(this.players[0].Groups["name"].Value, this.players[2].Groups["name"].Value);
                    this.P2 = new Player(this.players[3].Groups["name"].Value, this.players[5].Groups["name"].Value);
                }
            }

        }

        public Player P1 { get; }
        public Player P2 { get; }
    }
}
