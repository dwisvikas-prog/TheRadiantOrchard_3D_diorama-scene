namespace RadiantOrchard.Gameplay
{
    /// <summary>A visitor was healed: colour sweep, virtue pop-up and seeds follow (CLAUDE.md 5.6).</summary>
    public readonly struct VisitorHealed
    {
        public readonly string FruitId;
        public readonly string VirtueNameKey;
        public readonly int Seeds;
        public readonly int HealedTotal;

        public VisitorHealed(string fruitId, string virtueNameKey, int seeds, int healedTotal)
        {
            FruitId = fruitId;
            VirtueNameKey = virtueNameKey;
            Seeds = seeds;
            HealedTotal = healedTotal;
        }
    }
}
