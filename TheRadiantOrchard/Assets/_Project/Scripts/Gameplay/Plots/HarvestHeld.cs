namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The right gesture on a ripe tree, but the fruit has nowhere to go (nobody asked for it and its basket is full):
    /// it stays on the tree and a short "Basket full" note shows.
    /// </summary>
    public readonly struct HarvestHeld
    {
        public readonly int Plot;
        public readonly string FruitId;

        public HarvestHeld(int plot, string fruitId)
        {
            Plot = plot;
            FruitId = fruitId;
        }
    }
}
