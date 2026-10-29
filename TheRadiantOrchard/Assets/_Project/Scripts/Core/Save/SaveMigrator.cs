namespace RadiantOrchard.Core
{
    /// <summary>
    /// Brings a loaded save up to GameState.CurrentVersion one version at a time (v1 → v2 → v3...).
    /// When the schema changes: bump CurrentVersion, add one step below for the old version, never delete fields in place.
    /// </summary>
    public static class SaveMigrator
    {
        /// <summary>False if the save cannot be used: an invalid version, or one newer than this build understands.</summary>
        public static bool TryMigrate(GameState state, out string error)
        {
            if (state == null)
            {
                error = "no state";
                return false;
            }

            if (state.Version < 1)
            {
                error = $"invalid save version {state.Version}";
                return false;
            }

            // Refusing a newer save keeps an older app build from replacing the player's progress with a fresh game.
            if (state.Version > GameState.CurrentVersion)
            {
                error = $"save version {state.Version} is newer than this build ({GameState.CurrentVersion})";
                return false;
            }

            // Version 1 is the first schema, so there are no upgrade steps yet.
            error = null;
            return true;
        }
    }
}
