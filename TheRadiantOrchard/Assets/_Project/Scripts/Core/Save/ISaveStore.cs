namespace RadiantOrchard.Core
{
    /// <summary>
    /// Where the save JSON lives. LocalFileStore now; a cloud store can be added in Phase 2 without touching game code.
    /// </summary>
    public interface ISaveStore
    {
        /// <summary>The current save, or null if there is none.</summary>
        string Read();

        /// <summary>The previous good save, or null if the store keeps no backup.</summary>
        string ReadBackup();

        /// <summary>Stores the save so that a crash mid-write never leaves no readable save behind.</summary>
        void Write(string json);

        /// <summary>Removes the save and its backup (reset progress).</summary>
        void Delete();
    }
}
