namespace RadiantOrchard.Core
{
    /// <summary>The player's seed total changed (planting, healing, bonuses).</summary>
    public readonly struct SeedsChanged
    {
        public readonly int Seeds;
        public readonly int Delta;

        public SeedsChanged(int seeds, int delta)
        {
            Seeds = seeds;
            Delta = delta;
        }
    }
}
