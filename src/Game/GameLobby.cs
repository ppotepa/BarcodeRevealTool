namespace BarcodeRevealTool.Game
{

    /// <summary>
    /// as of now we wanna just support 1v1 mode, prolly the structure is a bit different for team games, also arcase, 
    /// would need dig deeper into the lobby file structure
    /// </summary>
    internal class GameLobby
    {
        private readonly byte[]? bytes;

        private const int PLAYER_TWO_STARTING_INDEX = 0x8D;
        private const int PLAYER_CHUNK_LENGTH = 0x8D;

        private Player? _p2;
        private Player? _p1;

        public Player P1
        {
            get
            {

                Span<byte> p1 = new Span<byte>(bytes).Slice(0x00006E0E, PLAYER_CHUNK_LENGTH);

                if (_p1 is null)
                {
                    var firstNick = new String([.. p1.Slice(0, 20).ToArray().Select(x => (char)x)]);
                    _p1 = new Player(p1);
                }

                return _p1;
            }
        }

        public Player P2
        {
            get
            {
                Span<byte> p2 = new Span<byte>(bytes).Slice(0x00006E0E + PLAYER_TWO_STARTING_INDEX, PLAYER_CHUNK_LENGTH);

                if (_p2 is null)
                {
                    var firstNick = new String([.. p2.Slice(0, 20).ToArray().Select(x => (char)x)]);
                    _p2 = new Player(p2);
                }

                return _p2;
            }
        }

        public GameLobby(byte[] bytes)
        {
            this.bytes = bytes;
        }
    }
}
