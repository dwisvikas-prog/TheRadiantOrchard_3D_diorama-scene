namespace RadiantOrchard.Gameplay
{
    /// <summary>A mini-event started (CLAUDE.md 5.11): the banner names it.</summary>
    public readonly struct MiniEventStarted
    {
        public readonly string Id;

        public MiniEventStarted(string id) => Id = id;
    }
}
