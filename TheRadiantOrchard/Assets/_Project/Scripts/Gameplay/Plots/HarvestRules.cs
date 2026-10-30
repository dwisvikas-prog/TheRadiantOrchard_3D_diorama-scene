using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Pure harvest rules (CLAUDE.md 5.5): a ripe tree is picked with its fruit's gesture (section 4); any other
    /// gesture on it is a wrong gesture. Only ripe trees react.
    /// </summary>
    public static class HarvestRules
    {
        public static HarvestJudgement Judge(PlotInfo plot, FruitDefinition fruit, GestureType gesture)
        {
            if (!plot.IsRipe || fruit == null) return HarvestJudgement.Ignore;
            return gesture == fruit.Gesture ? HarvestJudgement.Harvest : HarvestJudgement.WrongGesture;
        }

        /// <summary>
        /// A single tap on a ripe tree: for a double-tap fruit it is the first half of the right gesture, so no
        /// reaction; for long-press and swipe fruits it is a wrong gesture, which hints at trying something else.
        /// </summary>
        public static HarvestJudgement JudgeTap(PlotInfo plot, FruitDefinition fruit)
        {
            if (!plot.IsRipe || fruit == null || fruit.Gesture == GestureType.DoubleTap) return HarvestJudgement.Ignore;
            return HarvestJudgement.WrongGesture;
        }
    }
}
