namespace RadiantOrchard.Gameplay
{
    /// <summary>A visitor was healed: colour sweep, virtue pop-up and seeds follow (CLAUDE.md 5.6).</summary>
    public readonly struct VisitorHealed
    {
        /// <summary>The healed visitor, or -1 when none is on the path (dev heals).</summary>
        public readonly int VisitorId;
        public readonly string FruitId;
        public readonly string VirtueNameKey;
        public readonly int Seeds;
        public readonly int HealedTotal;

        public VisitorHealed(int visitorId, string fruitId, string virtueNameKey, int seeds, int healedTotal)
        {
            VisitorId = visitorId;
            FruitId = fruitId;
            VirtueNameKey = virtueNameKey;
            Seeds = seeds;
            HealedTotal = healedTotal;
        }
    }
}
