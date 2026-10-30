namespace RadiantOrchard.Gameplay
{
    /// <summary>A read-only snapshot of one plot for views and menus.</summary>
    public readonly struct PlotInfo
    {
        public readonly int Index;
        public readonly bool Unlocked;
        public readonly string FruitId;
        public readonly TreeStage Stage;
        public readonly int SecondsUntilRipe;
        public readonly float Progress;

        public PlotInfo(int index, bool unlocked, string fruitId, TreeStage stage, int secondsUntilRipe, float progress)
        {
            Index = index;
            Unlocked = unlocked;
            FruitId = fruitId;
            Stage = stage;
            SecondsUntilRipe = secondsUntilRipe;
            Progress = progress;
        }

        public bool IsEmpty => Stage == TreeStage.Empty;
        public bool IsRipe => Stage == TreeStage.Ripe;

        /// <summary>Planted but not ripe yet (sapling or growing): the plot shows a countdown (CLAUDE.md 5.3).</summary>
        public bool IsGrowing => Stage == TreeStage.Sapling || Stage == TreeStage.Growing;
    }
}
