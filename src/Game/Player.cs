namespace BarcodeRevealTool.Game
{
    internal class Player(string name, string tag)
    {
        public string NickName { get; } = name;
        public string Tag { get; } = tag;

        public override bool Equals(object? obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            Player? other = (Player?)obj;

            bool namesEqual = other.NickName.Equals(NickName);
            bool tagsEqual = other.NickName.Equals(Tag);

            return namesEqual && tagsEqual;
        }

        public override string ToString() => $"Name : {NickName}, BattleTag: {Tag}";
    }
}
