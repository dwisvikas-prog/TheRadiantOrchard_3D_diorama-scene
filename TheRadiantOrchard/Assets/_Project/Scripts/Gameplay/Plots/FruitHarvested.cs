namespace RadiantOrchard.Gameplay
{
    /// <summary>A ripe fruit was picked; delivery and healing take it from here.</summary>
    public readonly struct FruitHarvested
    {
        public readonly int Plot;
        public readonly string FruitId;
        /// <summary>A golden fruit (CLAUDE.md 5.12): its heal pays GoldenFruitSeedMultiplier times the seeds.</summary>
        public readonly bool IsGolden;

        public FruitHarvested(int plot, string fruitId, bool isGolden = false)
        {
            Plot = plot;
            FruitId = fruitId;
            IsGolden = isGolden;
        }
    }
}
