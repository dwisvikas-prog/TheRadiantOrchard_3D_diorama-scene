namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Golden fruit (CLAUDE.md 5.12): about GoldenFruitChance of ripe fruits are golden. Decided from the plot and the
    /// start of its growth cycle, so the same fruit is golden every time it is looked at, across restarts, without
    /// saving anything, and the player can see it on the tree before picking it. Pure.
    /// </summary>
    public static class GoldenFruitRules
    {
        public static bool IsGolden(int plot, long cycleStartUtc, float chance)
        {
            if (chance <= 0f || cycleStartUtc <= 0) return false;
            return Roll(plot, cycleStartUtc) < chance;
        }

        /// <summary>An even spread in [0, 1) from the plot and cycle start (SplitMix64 finaliser).</summary>
        public static double Roll(int plot, long cycleStartUtc)
        {
            unchecked
            {
                ulong z = (ulong)cycleStartUtc * 31UL + (ulong)(uint)plot + 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= z >> 31;
                return (z >> 11) * (1.0 / (1UL << 53));
            }
        }
    }
}
