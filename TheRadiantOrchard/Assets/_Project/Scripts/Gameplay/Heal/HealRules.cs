using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure healing rules (CLAUDE.md 5.6). For now: a visitor is healed only by the fruit they asked for, and a heal
    /// pays BalanceConfig.SeedsPerHeal. Bench (5.4), Golden Harvest (5.8), golden fruit (5.12) and combo (5.13)
    /// rewards are added here in Week 2.
    /// </summary>
    public static class HealRules
    {
        public static bool Accepts(string requestedFruitId, string deliveredFruitId) =>
            !string.IsNullOrEmpty(requestedFruitId) && requestedFruitId == deliveredFruitId;

        public static int SeedsFor(BalanceConfig balance) => balance.SeedsPerHeal;
    }
}
