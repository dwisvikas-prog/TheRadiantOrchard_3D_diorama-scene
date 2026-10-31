using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure vibrancy rules (CLAUDE.md 5.7): total heals → stage (BalanceConfig.StageThresholds), and how far the meter
    /// is through the current stage. The bar measures one stage at a time so it moves visibly from the first heal.
    /// </summary>
    public static class VibrancyRules
    {
        /// <summary>Number of stage thresholds reached by this many heals (0..5).</summary>
        public static int StageFor(BalanceConfig balance, int healed)
        {
            int stage = 0;
            while (stage < balance.StageThresholds.Count && healed >= balance.StageThresholds[stage]) stage++;
            return stage;
        }

        /// <summary>Progress from the current stage's threshold to the next one.</summary>
        public static VibrancyProgress Progress(BalanceConfig balance, int healed, int stage)
        {
            int last = balance.StageThresholds.Count;
            stage = Math.Max(0, Math.Min(stage, last));
            if (stage >= last) return new VibrancyProgress(stage, 0, 0, true);

            int from = stage == 0 ? 0 : balance.HealsToReach(stage);
            int needed = balance.HealsToReach(stage + 1) - from;
            int done = Math.Max(0, Math.Min(healed - from, needed));
            return new VibrancyProgress(stage, done, needed, false);
        }
    }
}
