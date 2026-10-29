using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure growth rules: the stage and time left come only from saved UTC timestamps and "now", so trees keep growing
    /// while the app is closed. A first planting goes sapling → growing → ripe; after a harvest the tree is already
    /// full size, so it regrows growing → ripe on the same timer.
    /// </summary>
    public static class GrowthCalculator
    {
        /// <summary>When the current growth cycle started: planting, or the last harvest if later.</summary>
        public static long CycleStartUtc(PlotState plot) => Math.Max(plot.PlantedUtc, plot.LastHarvestUtc);

        /// <summary>0..1 through the current cycle. A start in the future (clock turned back) counts as 0.</summary>
        public static double Progress(PlotState plot, FruitDefinition fruit, long nowUtcMs)
        {
            if (plot == null || fruit == null || !plot.IsPlanted) return 0d;
            long elapsedMs = Math.Max(0L, nowUtcMs - CycleStartUtc(plot));
            return Math.Min(1d, elapsedMs / (fruit.GrowSeconds * 1000d));
        }

        public static TreeStage StageAt(PlotState plot, FruitDefinition fruit, long nowUtcMs, float saplingFraction)
        {
            if (plot == null || fruit == null || !plot.IsPlanted) return TreeStage.Empty;

            double progress = Progress(plot, fruit, nowUtcMs);
            if (progress >= 1d) return TreeStage.Ripe;

            bool regrowing = plot.LastHarvestUtc > 0;
            return !regrowing && progress < saplingFraction ? TreeStage.Sapling : TreeStage.Growing;
        }

        /// <summary>Whole seconds until ripe (0 when ripe or empty). Rounded up so a timer never shows 0 before ripe.</summary>
        public static int SecondsUntilRipe(PlotState plot, FruitDefinition fruit, long nowUtcMs)
        {
            if (plot == null || fruit == null || !plot.IsPlanted) return 0;
            long elapsedMs = Math.Max(0L, nowUtcMs - CycleStartUtc(plot));
            long leftMs = fruit.GrowSeconds * 1000L - elapsedMs;
            return leftMs <= 0 ? 0 : (int)((leftMs + 999) / 1000);
        }
    }
}
