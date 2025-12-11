using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories.Abstractions
{
    /// <summary>
    /// Specialized repository interface for user account queries.
    /// </summary>
    public interface IUserAccountRepository : IRepository<UserAccountEntity>
    {
        /// <summary>Get account by battle tag.</summary>
        Task<UserAccountEntity?> GetByBattleTagAsync(string battleTag);

        /// <summary>Get all user accounts.</summary>
        Task<IReadOnlyList<UserAccountEntity>> GetAllAccountsAsync();
    }
}
