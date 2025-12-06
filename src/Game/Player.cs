using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Game
{
    internal class Player
    {

        public const string UNKNOWN = "UNKNOWN";

        private const short NICKNAME_START_INDEX = 0;
        private const short BATTLE_TAG_START_INDEX = 82;

        public Regex NickNamePattern = new Regex("(?<nick>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");
        public Regex BattleTagPattern = new Regex("(?<nick>[A-Za-z][A-Za-z0-9]{2,20}#[0-9]{3,6})");

        private string _battleTag;
        private string _nickname;

        public string NickName { get; }
        public string BattleTag { get; }

        public Player(Span<byte> playerSpan)
        {
            this.NickName = new String([.. playerSpan.Slice(NICKNAME_START_INDEX, 18).ToArray().Select(c => (char)c)]);
            this.BattleTag = new String([.. playerSpan.Slice(BATTLE_TAG_START_INDEX, 22).ToArray().Select(c => (char)c)]);
        }

        public override string ToString()
        {
            var nick = NickNamePattern.Match(NickName).Groups["nick"]?.Value;
            var tag = BattleTagPattern.Match(BattleTag).Groups["nick"]?.Value;

            return new string(
                @$"[NICK : {nick ?? UNKNOWN} TAG:{tag ?? UNKNOWN}]"
            );
        }

        public override bool Equals(object? obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            Player? other = (Player?)obj;

            bool namesEqual = other.NickName.Equals(NickName);
            bool tagsEqual = other.NickName.Equals(BattleTag);

            return namesEqual && tagsEqual;
        }
    }
}
