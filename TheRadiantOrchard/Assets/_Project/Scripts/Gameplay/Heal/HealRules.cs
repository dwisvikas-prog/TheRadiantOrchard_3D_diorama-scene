using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure healing rules (CLAUDE.md 5.6, 5.12, 5.13). A visitor is healed only by the fruit they asked for. Seeds =
    /// base (SeedsPerHeal, or SeedsPerBenchHeal on the bench, 5.4) × golden fruit (GoldenFruitSeedMultiplier) × boosts
    /// (Golden Harvest, 5.8) × combo (its count, at most ComboMaxMultiplier). The bonuses multiply (agreed 31 Oct).
    /// </summary>
    public static class HealRules
    {
        public static bool Accepts(string requestedFruitId, string deliveredFruitId) =>
            !string.IsNullOrEmpty(requestedFruitId) && requestedFruitId == deliveredFruitId;

        /// <summary>Seeds before golden fruit, boosts and combo.</summary>
        public static int BaseSeeds(BalanceConfig balance, bool fromBench) =>
            fromBench ? balance.SeedsPerBenchHeal : balance.SeedsPerHeal;

        public static int SeedsFor(BalanceConfig balance, bool fromBench = false, bool golden = false, float boost = 1f, int combo = 1)
        {
            double seeds = BaseSeeds(balance, fromBench);
            if (golden) seeds *= balance.GoldenFruitSeedMultiplier;
            seeds *= Math.Max(0f, boost);
            seeds *= ComboMultiplier(balance, combo);
            return (int)Math.Round(seeds, MidpointRounding.AwayFromZero);
        }

        /// <summary>The combo's seed multiplier: its count, between 1 and ComboMaxMultiplier.</summary>
        public static int ComboMultiplier(BalanceConfig balance, int combo) =>
            Math.Max(1, Math.Min(combo, balance.ComboMaxMultiplier));
    }
}
