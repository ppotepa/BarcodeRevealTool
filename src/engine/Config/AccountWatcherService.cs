using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace BarcodeRevealTool.Engine.Config
{
    /// <summary>
    /// Monitors SC2 account folder for new .lnk files periodically (every 60 seconds).
    /// Only checks for NEW files instead of re-discovering entire folder structure.
    /// </summary>
    public class AccountWatcherService : IDisposable
    {
        private readonly string _accountsFolder;
        private bool _disposed = false;
        private DateTime _lastCheckTime = DateTime.MinValue;
        private const int CheckIntervalSeconds = 60; // Check every 60 seconds for new .lnk files
        private Timer? _checkTimer;
        private HashSet<string> _lastKnownLnkFiles = new();
        private bool _isRunning = false;

        public event EventHandler<AccountsChangedEventArgs>? AccountsChanged;

        public AccountWatcherService()
        {
            _accountsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "StarCraft II",
                "Accounts"
            );

            System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Initialized for folder: {_accountsFolder}");
        }

        /// <summary>
        /// Start monitoring for new .lnk files (checks every 60 seconds).
        /// </summary>
        public void StartWatching()
        {
            if (!Directory.Exists(_accountsFolder))
            {
                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Accounts folder does not exist: {_accountsFolder}");
                return;
            }

            if (_isRunning)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Already running");
                return;
            }

            try
            {
                // Initialize with current .lnk files
                UpdateLnkFileList();

                // Start timer to check every 60 seconds
                _checkTimer = new Timer(CheckForNewLnkFiles, null, TimeSpan.FromSeconds(CheckIntervalSeconds), TimeSpan.FromSeconds(CheckIntervalSeconds));
                _isRunning = true;

                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Started periodic checking (every {CheckIntervalSeconds} seconds)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Error starting watcher: {ex.Message}");
                _checkTimer?.Dispose();
                _checkTimer = null;
                _isRunning = false;
            }
        }

        /// <summary>
        /// Stop monitoring for new .lnk files.
        /// </summary>
        public void StopWatching()
        {
            if (_checkTimer != null)
            {
                _checkTimer.Dispose();
                _checkTimer = null;
                _isRunning = false;
                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Stopped checking");
            }
        }

        /// <summary>
        /// Get all .lnk files in the accounts folder recursively.
        /// </summary>
        private List<string> GetAllLnkFiles()
        {
            var lnkFiles = new List<string>();

            try
            {
                if (!Directory.Exists(_accountsFolder))
                    return lnkFiles;

                var files = Directory.GetFiles(_accountsFolder, "*.lnk", SearchOption.AllDirectories);
                lnkFiles = files.ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Error scanning for .lnk files: {ex.Message}");
            }

            return lnkFiles;
        }

        /// <summary>
        /// Update the list of known .lnk files.
        /// </summary>
        private void UpdateLnkFileList()
        {
            var currentLnkFiles = GetAllLnkFiles();
            _lastKnownLnkFiles = new HashSet<string>(currentLnkFiles);
            System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Initialized with {_lastKnownLnkFiles.Count} .lnk files");
        }

        /// <summary>
        /// Called periodically to check if there are any new .lnk files.
        /// </summary>
        private void CheckForNewLnkFiles(object? state)
        {
            try
            {
                var currentLnkFiles = GetAllLnkFiles();
                var currentSet = new HashSet<string>(currentLnkFiles);

                // Find new files
                var newFiles = currentSet.Except(_lastKnownLnkFiles).ToList();

                if (newFiles.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Found {newFiles.Count} new .lnk file(s)");
                    _lastKnownLnkFiles = currentSet;
                    OnAccountsChanged(new AccountsChangedEventArgs { ChangedAt = DateTime.UtcNow, NewLnkFilesCount = newFiles.Count });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Error checking for new .lnk files: {ex.Message}");
            }
        }

        protected virtual void OnAccountsChanged(AccountsChangedEventArgs e)
        {
            AccountsChanged?.Invoke(this, e);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopWatching();
            _disposed = true;
            System.Diagnostics.Debug.WriteLine($"[AccountWatcher] Disposed");
        }
    }

    /// <summary>
    /// Event arguments for account changes.
    /// </summary>
    public class AccountsChangedEventArgs : EventArgs
    {
        public DateTime ChangedAt { get; set; }
        public int NewLnkFilesCount { get; set; }
    }
}
