using Serilog;
using BarcodeRevealTool.Persistence.Repositories.Abstractions;
using BarcodeRevealTool.Persistence.Repositories.Entities;

namespace BarcodeRevealTool.Persistence.Cache
{
    /// <summary>
    /// Service for initializing user account information on application startup.
    /// Populates the UserAccounts table with the current user's account from configuration.
    /// </summary>
    public class UserAccountInitializationService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly UserConfigService _configService;
        private readonly ILogger _logger;

        public UserAccountInitializationService(IUnitOfWork unitOfWork, UserConfigService configService)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _logger = Log.ForContext<UserAccountInitializationService>();
        }

        /// <summary>
        /// Initializes user account information from configuration.
        /// If the user account doesn't exist in the database, creates it with data from UserConfig.
        /// </summary>
        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get user's BattleTag from configuration
                var userBattleTag = _configService.GetConfig("UserBattleTag");

                if (string.IsNullOrEmpty(userBattleTag))
                {
                    _logger.Warning("UserBattleTag not configured, skipping UserAccount initialization");
                    return;
                }

                // Check if account already exists
                var existingAccounts = await _unitOfWork.UserAccounts
                    .GetAllAsync(a => a.BattleTag == userBattleTag, cancellationToken);

                if (existingAccounts.Any())
                {
                    _logger.Debug("UserAccount already exists for {BattleTag}", userBattleTag);
                    return;
                }

                // Create new user account from config
                var newAccount = new UserAccountEntity
                {
                    BattleTag = userBattleTag,
                    AccountName = _configService.GetConfig("AccountName") ?? "Unknown",
                    Realm = _configService.GetConfig("Realm") ?? "Unknown",
                    Region = _configService.GetConfig("Region") ?? "Unknown"
                };

                // Try to parse AccountId if present
                if (int.TryParse(_configService.GetConfig("AccountId"), out int accountId))
                {
                    newAccount.AccountId = accountId;
                }

                var newId = await _unitOfWork.UserAccounts.AddAsync(newAccount, cancellationToken);
                _logger.Information("Initialized UserAccount {BattleTag} with ID {AccountId}", userBattleTag, newId);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to initialize UserAccount");
                // Don't throw - allow application to continue even if initialization fails
            }
        }
    }
}
