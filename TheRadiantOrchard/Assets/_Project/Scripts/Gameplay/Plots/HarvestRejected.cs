namespace RadiantOrchard.Gameplay
{
    /// <summary>A ripe tree got the wrong gesture: the tree wobbles and a soft sound plays (CLAUDE.md 5.5).</summary>
    public readonly struct HarvestRejected
    {
        public readonly int Plot;
        public readonly string FruitId;

        public HarvestRejected(int plot, string fruitId)
        {
            Plot = plot;
            FruitId = fruitId;
        }
    }
}
