using System.Diagnostics;

namespace BarcodeRevealTool.Engine
{
    /// <summary>
    /// Manages game process state detection and provides state change events.
    /// Tracks whether StarCraft II is running and when the game lobby is loaded.
    /// </summary>
    public class GameStateManager
    {
        public enum GameProcessState
        {
            /// <summary>StarCraft II is not running</summary>
            NotRunning,
            /// <summary>StarCraft II is running but no match detected</summary>
            Running,
            /// <summary>StarCraft II is running and match/lobby detected</summary>
            InMatch
        }

        private GameProcessState _currentState = GameProcessState.NotRunning;

        /// <summary>
        /// Fired when the game process state changes
        /// </summary>
        public event EventHandler<GameProcessStateChangedEventArgs>? GameProcessStateChanged;

        /// <summary>
        /// Gets the current game process state
        /// </summary>
        public GameProcessState CurrentState => _currentState;

        /// <summary>
        /// Gets whether StarCraft II process is currently running
        /// </summary>
        public bool IsGameRunning => _currentState != GameProcessState.NotRunning;

        /// <summary>
        /// Check if StarCraft II is running (looks for SC2_x64.exe or SC2.exe)
        /// </summary>
        public bool IsStarCraft2Running()
        {
            try
            {
                // SC2_x64.exe is the 64-bit process (modern versions)
                // SC2.exe is the 32-bit process (legacy)
                return Process.GetProcessesByName("SC2_x64").Length > 0 ||
                       Process.GetProcessesByName("SC2").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Update the game state and fire events if state changed
        /// </summary>
        public void UpdateGameProcessState(bool hasLobbyFile)
        {
            var isRunning = IsStarCraft2Running();
            var newState = isRunning
                ? (hasLobbyFile ? GameProcessState.InMatch : GameProcessState.Running)
                : GameProcessState.NotRunning;

            if (newState != _currentState)
            {
                var oldState = _currentState;
                _currentState = newState;
                OnGameProcessStateChanged(oldState, newState);
            }
        }

        protected virtual void OnGameProcessStateChanged(GameProcessState oldState, GameProcessState newState)
        {
            GameProcessStateChanged?.Invoke(this, new GameProcessStateChangedEventArgs(oldState, newState));
        }
    }

    /// <summary>
    /// Event args for game process state changes
    /// </summary>
    public class GameProcessStateChangedEventArgs : EventArgs
    {
        public GameStateManager.GameProcessState OldState { get; }
        public GameStateManager.GameProcessState NewState { get; }

        public GameProcessStateChangedEventArgs(GameStateManager.GameProcessState oldState, GameStateManager.GameProcessState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }
}
