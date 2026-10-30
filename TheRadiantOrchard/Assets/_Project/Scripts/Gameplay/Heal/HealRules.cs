using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure healing rules (CLAUDE.md 5.6). A visitor is healed only by the fruit they asked for; a heal pays
    /// BalanceConfig.SeedsPerHeal, or SeedsPerBenchHeal for a visitor on the bench (5.4). Golden Harvest (5.8),
    /// golden fruit (5.12) and combo (5.13) rewards are added here in Week 2.
    /// </summary>
    public static class HealRules
    {
        public static bool Accepts(string requestedFruitId, string deliveredFruitId) =>
            !string.IsNullOrEmpty(requestedFruitId) && requestedFruitId == deliveredFruitId;

        public static int SeedsFor(BalanceConfig balance, bool fromBench = false) =>
            fromBench ? balance.SeedsPerBenchHeal : balance.SeedsPerHeal;
    }
}
