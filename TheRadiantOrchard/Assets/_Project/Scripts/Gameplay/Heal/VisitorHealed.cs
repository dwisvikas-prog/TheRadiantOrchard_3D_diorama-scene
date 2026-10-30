namespace RadiantOrchard.Gameplay
{
    /// <summary>A visitor was healed: colour sweep, virtue and seed pop-ups and the combo counter follow (CLAUDE.md 5.6).</summary>
    public readonly struct VisitorHealed
    {
        /// <summary>The healed visitor, or -1 when none is on the path (dev heals).</summary>
        public readonly int VisitorId;
        public readonly string FruitId;
        public readonly string VirtueNameKey;
        /// <summary>Seeds paid, all bonuses included.</summary>
        public readonly int Seeds;
        public readonly int HealedTotal;
        /// <summary>Seeds before golden fruit, boosts and combo (10, or 5 on the bench).</summary>
        public readonly int BaseSeeds;
        /// <summary>The combo's multiplier for this heal (1 = no combo).</summary>
        public readonly int Combo;
        public readonly bool Golden;
        public readonly bool FromBench;

        public VisitorHealed(int visitorId, string fruitId, string virtueNameKey, int seeds, int healedTotal,
            int baseSeeds = 0, int combo = 1, bool golden = false, bool fromBench = false)
        {
            VisitorId = visitorId;
            FruitId = fruitId;
            VirtueNameKey = virtueNameKey;
            Seeds = seeds;
            HealedTotal = healedTotal;
            BaseSeeds = baseSeeds;
            Combo = combo;
            Golden = golden;
            FromBench = fromBench;
        }

        /// <summary>Paid more than the plain heal: golden fruit, a boost or a combo.</summary>
        public bool HasBonus => Seeds > BaseSeeds;
    }
}
