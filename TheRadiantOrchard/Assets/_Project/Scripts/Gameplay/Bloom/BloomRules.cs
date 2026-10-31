using System;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// When a fruit on the blooming Wisdom Tree is ripe (CLAUDE.md 5.9): never picked, or picked at least the regrow
    /// time ago. A pick that looks like it is in the future (clock turned back) waits, with no other penalty.
    /// Times are Unix milliseconds UTC; 0 = never picked.
    /// </summary>
    public static class BloomRules
    {
        public static bool IsRipe(long lastHarvestUtc, long nowUtc, int regrowSeconds) =>
            lastHarvestUtc <= 0 || (nowUtc >= lastHarvestUtc && nowUtc - lastHarvestUtc >= regrowSeconds * 1000L);

        /// <summary>Whole seconds until the fruit is ripe again, rounded up; 0 when ripe.</summary>
        public static int SecondsUntilRipe(long lastHarvestUtc, long nowUtc, int regrowSeconds)
        {
            if (IsRipe(lastHarvestUtc, nowUtc, regrowSeconds)) return 0;
            if (nowUtc < lastHarvestUtc) return regrowSeconds;
            long leftMs = regrowSeconds * 1000L - (nowUtc - lastHarvestUtc);
            return (int)Math.Ceiling(leftMs / 1000.0);
        }
    }
}
