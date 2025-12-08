using System.Text.RegularExpressions;

namespace BarcodeRevealTool.Engine.Config
{
    /// <summary>
    /// Discovers all toon handles (player accounts) from SC2 account folder structure.
    /// The SC2 account folder contains subdirectories named after toon handles: 1-S2-1-13242825, 2-S2-1-2727568, etc.
    /// These correspond to the .lnk files in Documents\StarCraft II
    /// </summary>
    public class AccountToonDiscoveryService
    {
        private static readonly string SC2_ACCOUNT_PATH = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "StarCraft II",
            "Accounts"
        );

        /// <summary>
        /// Discover all toon handles from the SC2 account folder structure.
        /// Returns a list of toon handles like: ["1-S2-1-13242825", "2-S2-1-2727568"]
        /// </summary>
        public static List<string> DiscoverAllToonHandles()
        {
            var toonHandles = new List<string>();

            if (!Directory.Exists(SC2_ACCOUNT_PATH))
            {
                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] SC2 account path not found: {SC2_ACCOUNT_PATH}");
                return toonHandles;
            }

            try
            {
                // Get all subdirectories in the Accounts folder
                var accountDirs = Directory.GetDirectories(SC2_ACCOUNT_PATH);
                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Found {accountDirs.Length} account directories");

                foreach (var accountDir in accountDirs)
                {
                    // Each account directory contains toon handle folders
                    var toonDirs = Directory.GetDirectories(accountDir);

                    foreach (var toonDir in toonDirs)
                    {
                        var dirName = Path.GetFileName(toonDir);

                        // Toon handles match pattern: region-S2-realm-id (e.g., 1-S2-1-13242825)
                        if (IsToonHandle(dirName))
                        {
                            toonHandles.Add(dirName);
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Total toons discovered: {toonHandles.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Error discovering toons: {ex.Message}");
            }

            return toonHandles;
        }

        /// <summary>
        /// Build a mapping of toon handles to their nick names and battle tags from .lnk files.
        /// Returns a dictionary: toon handle → (nick name, discriminator)
        /// Example: "2-S2-1-9928025" → ("Admin", "404")
        /// </summary>
        public static Dictionary<string, (string nick, string discriminator)> DiscoverToonNickMapping()
        {
            var mapping = new Dictionary<string, (string, string)>();

            // Get the base SC2 path (parent of "Accounts")
            var sc2BasePath = Path.GetDirectoryName(SC2_ACCOUNT_PATH);
            if (string.IsNullOrEmpty(sc2BasePath) || !Directory.Exists(sc2BasePath))
            {
                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] SC2 base path not found");
                return mapping;
            }

            try
            {
                // Look for .lnk files in the StarCraft II folder
                var linkFiles = Directory.GetFiles(sc2BasePath, "*.lnk");
                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Found {linkFiles.Length} .lnk files for nick mapping");

                foreach (var linkFile in linkFiles)
                {
                    var filename = Path.GetFileNameWithoutExtension(linkFile);
                    // Format: NickName_RealmID@RegionID
                    // Example: Admin_404@2 or Admin_404@2

                    var match = Regex.Match(filename, @"^(.+?)_(\d+)@(\d+)$");
                    if (match.Success)
                    {
                        var nick = match.Groups[1].Value;
                        var discriminator = match.Groups[2].Value;
                        var region = match.Groups[3].Value;

                        System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Mapped .lnk: {filename} → Nick={nick}, Discriminator={discriminator}, Region={region}");

                        // Now find the toon handle in the Accounts folder that corresponds to this region and realm
                        // Try to match based on the region from the .lnk file
                        var allToons = DiscoverAllToonHandles();
                        foreach (var toon in allToons)
                        {
                            var toonRegion = ExtractRegion(toon);
                            if (toonRegion == region && !mapping.ContainsKey(toon))
                            {
                                // For now, assume the first toon in the target region matches this nick
                                // A more sophisticated approach would check the account folder structure
                                mapping[toon] = (nick, discriminator);
                                break;
                            }
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Total nick mappings created: {mapping.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountToonDiscovery] Error discovering toon nick mapping: {ex.Message}");
            }

            return mapping;
        }

        /// <summary>
        /// Check if a directory name matches the toon handle pattern.
        /// Pattern: region-S2-realm-id where region, realm, id are numbers
        /// Examples: 1-S2-1-13242825, 2-S2-1-2727568
        /// </summary>
        private static bool IsToonHandle(string dirName)
        {
            // Match pattern: number-S2-number-number
            var match = Regex.Match(dirName, @"^\d+-S2-\d+-\d+$");
            return match.Success;
        }

        /// <summary>
        /// Extract region code from toon handle.
        /// Example: "1-S2-1-13242825" → "1" (Americas)
        /// </summary>
        public static string? ExtractRegion(string toonHandle)
        {
            var match = Regex.Match(toonHandle, @"^(\d+)-S2-");
            return match.Success ? match.Groups[1].Value : null;
        }

        /// <summary>
        /// Extract realm code from toon handle.
        /// Example: "1-S2-2-13242825" → "2"
        /// </summary>
        public static string? ExtractRealm(string toonHandle)
        {
            var match = Regex.Match(toonHandle, @"^(\d+)-S2-(\d+)-");
            return match.Success ? match.Groups[2].Value : null;
        }

        /// <summary>
        /// Extract battle.net ID from toon handle.
        /// Example: "1-S2-1-13242825" → "13242825"
        /// </summary>
        public static string? ExtractBattleNetId(string toonHandle)
        {
            var match = Regex.Match(toonHandle, @"-(\d+)$");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
