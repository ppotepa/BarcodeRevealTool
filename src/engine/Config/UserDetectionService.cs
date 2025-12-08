using System.Text.RegularExpressions;
using BarcodeRevealTool.Game;

namespace BarcodeRevealTool.Engine.Config
{
    /// <summary>
    /// Detects the current user account by scanning StarCraft II account link files.
    /// Links are stored in: C:\Users\{username}\Documents\StarCraft II
    /// Format: AccountName_RealmID@RegionID.lnk (e.g., Ignacy_236@2.lnk)
    /// </summary>
    public class UserDetectionService
    {
        private static readonly string[] SC2_ACCOUNT_PATHS = new[]
        {
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "StarCraft II"
            ),
            // Fallback for alternate SC2 install locations
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Starcraft II"
            )
        };

        /// <summary>
        /// Detect user account from StarCraft II link files.
        /// Returns the battle tag in format "AccountName_RealmID" (e.g., "Ignacy_236")
        /// </summary>
        public static string? DetectUserAccount()
        {
            foreach (var accountPath in SC2_ACCOUNT_PATHS)
            {
                if (!Directory.Exists(accountPath))
                    continue;

                System.Diagnostics.Debug.WriteLine($"[UserDetection] Scanning {accountPath} for account links");

                try
                {
                    var linkFiles = Directory.GetFiles(accountPath, "*.lnk");

                    if (linkFiles.Length == 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UserDetection] No .lnk files found in {accountPath}");
                        continue;
                    }

                    System.Diagnostics.Debug.WriteLine($"[UserDetection] Found {linkFiles.Length} .lnk files");

                    // Extract battle tags from filenames
                    var battleTags = linkFiles
                        .Select(Path.GetFileNameWithoutExtension)
                        .Select(filename => ExtractBattleTag(filename))
                        .Where(tag => !string.IsNullOrEmpty(tag))
                        .Distinct()
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"[UserDetection] Extracted {battleTags.Count} unique battle tags: {string.Join(", ", battleTags)}");

                    // Return the first (or most recent) account found
                    // Could be enhanced to detect most-recently-played account if multiple exist
                    if (battleTags.Count > 0)
                    {
                        var selectedTag = battleTags[0];
                        System.Diagnostics.Debug.WriteLine($"[UserDetection] Selected user account: {selectedTag}");
                        return selectedTag;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UserDetection] Error scanning account path: {ex.Message}");
                }
            }

            System.Diagnostics.Debug.WriteLine($"[UserDetection] No user account detected");
            return null;
        }

        /// <summary>
        /// Extract battle tag from link filename.
        /// Format: AccountName_RealmID@RegionID.lnk
        /// Returns: AccountName_RealmID (e.g., "Ignacy_236" from "Ignacy_236@2.lnk")
        /// </summary>
        private static string? ExtractBattleTag(string filename)
        {
            // Match pattern: anything_number@number
            // Return just the account_realm part before the @
            var match = Regex.Match(filename, @"^(.+_\d+)@\d+$");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// Try to identify opponent from lobby by comparing with detected user account.
        /// Returns the opponent player, or null if user not found in lobby.
        /// Handles both raw format (Ignacy_236) and display format (Ignacy#236)
        /// Also matches by name prefix to handle same player with different realms (Ignacy#1 vs Ignacy_236)
        /// </summary>
        public static Player? IdentifyOpponent(Team? team1, Team? team2, string? userBattleTag)
        {
            if (string.IsNullOrEmpty(userBattleTag) || team1 == null || team2 == null)
                return null;

            System.Diagnostics.Debug.WriteLine($"[UserDetection] Identifying opponent for user: {userBattleTag}");

            // Normalize the user battle tag (convert _ to # for display format matching)
            string displayBattleTag = userBattleTag.Replace('_', '#');

            // Extract the name prefix (everything before the underscore/hashtag)
            string? namePrefix = ExtractNamePrefix(userBattleTag);
            System.Diagnostics.Debug.WriteLine($"[UserDetection] Looking for user with tag: {userBattleTag} or display: {displayBattleTag}, name prefix: {namePrefix}");

            // Check if user is in team1
            var userInTeam1 = team1.Players.FirstOrDefault(p =>
                p.Tag.Contains(userBattleTag, StringComparison.OrdinalIgnoreCase) ||
                p.Tag.Contains(displayBattleTag, StringComparison.OrdinalIgnoreCase) ||
                p.NickName.Contains(displayBattleTag, StringComparison.OrdinalIgnoreCase) ||
                (namePrefix != null && p.NickName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)));
            if (userInTeam1 != null)
            {
                var opponent = team2.Players.FirstOrDefault();
                System.Diagnostics.Debug.WriteLine($"[UserDetection] User found in Team1 ({userInTeam1.Tag} / {userInTeam1.NickName}), opponent: {opponent?.Tag}");
                return opponent;
            }

            // Check if user is in team2
            var userInTeam2 = team2.Players.FirstOrDefault(p =>
                p.Tag.Contains(userBattleTag, StringComparison.OrdinalIgnoreCase) ||
                p.Tag.Contains(displayBattleTag, StringComparison.OrdinalIgnoreCase) ||
                p.NickName.Contains(displayBattleTag, StringComparison.OrdinalIgnoreCase) ||
                (namePrefix != null && p.NickName.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase)));
            if (userInTeam2 != null)
            {
                var opponent = team1.Players.FirstOrDefault();
                System.Diagnostics.Debug.WriteLine($"[UserDetection] User found in Team2 ({userInTeam2.Tag} / {userInTeam2.NickName}), opponent: {opponent?.Tag}");
                return opponent;
            }

            System.Diagnostics.Debug.WriteLine($"[UserDetection] User not found in lobby");
            return null;
        }

        /// <summary>
        /// Extract the name prefix from a battle tag.
        /// Example: "Ignacy_236" → "Ignacy"
        /// </summary>
        private static string? ExtractNamePrefix(string battleTag)
        {
            if (string.IsNullOrEmpty(battleTag))
                return null;

            var underscoreIndex = battleTag.IndexOf('_');
            if (underscoreIndex > 0)
            {
                return battleTag.Substring(0, underscoreIndex);
            }

            return null;
        }
    }
}
