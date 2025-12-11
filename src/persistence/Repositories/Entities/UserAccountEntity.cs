namespace BarcodeRevealTool.Persistence.Repositories.Entities
{
    /// <summary>
    /// Represents a user account in the database.
    /// </summary>
    public class UserAccountEntity : BaseEntity
    {
        public string? BattleTag { get; set; }
        public string? AccountName { get; set; }
        public string? Realm { get; set; }
        public string? Region { get; set; }
        public int AccountId { get; set; }
    }
}
