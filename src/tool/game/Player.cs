namespace BarcodeRevealTool.Game
{
    public record Player()
    {
        public Player(string nickName, string tag) : this()
        {
            NickName = nickName ?? throw new ArgumentNullException(nameof(nickName));
            Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        }

        public required string NickName { get; set; }
        public required string Tag { get; set; }
        public override string ToString() => $"Name : {NickName}, BattleTag: {Tag}";
    }
}