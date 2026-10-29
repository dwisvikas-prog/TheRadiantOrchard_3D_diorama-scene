namespace RadiantOrchard.Core
{
    /// <summary>
    /// Slots the Dev assembly fills at startup (it only exists in the Editor and development builds). Release builds
    /// leave them empty, so nothing here can change how a release behaves. Core never references the Dev assembly.
    /// </summary>
    public static class DevHooks
    {
        public static IDevTimeOffsetStore TimeOffsetStore { get; set; }
    }
}
