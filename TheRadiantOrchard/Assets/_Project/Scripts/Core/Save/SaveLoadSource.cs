namespace RadiantOrchard.Core
{
    /// <summary>Where SaveService.Load found the game state.</summary>
    public enum SaveLoadSource
    {
        None = 0,
        Main = 1,
        Backup = 2,
        New = 3,
    }
}
