namespace RadiantOrchard.Gameplay
{
    /// <summary>A ripe fruit was picked; delivery and healing take it from here.</summary>
    public readonly struct FruitHarvested
    {
        public readonly int Plot;
        public readonly string FruitId;

        public FruitHarvested(int plot, string fruitId)
        {
            Plot = plot;
            FruitId = fruitId;
        }
    }
}
