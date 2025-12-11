using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Repositories
{
    /// <summary>
    /// Specialized repository for user account operations.
    /// </summary>
    public class UserAccountRepository : Repository<UserAccountEntity>, IUserAccountRepository
    {
        public UserAccountRepository(string connectionString) : base(connectionString)
        {
        }

        public async Task<UserAccountEntity?> GetByBattleTagAsync(string battleTag)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(a =>
                a.BattleTag?.Equals(battleTag, StringComparison.OrdinalIgnoreCase) ?? false
            );
        }

        public async Task<IReadOnlyList<UserAccountEntity>> GetAllAccountsAsync()
        {
            return await GetAllAsync();
        }
    }
}
