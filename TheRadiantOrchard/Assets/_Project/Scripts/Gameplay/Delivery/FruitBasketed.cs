namespace RadiantOrchard.Gameplay
{
    /// <summary>A harvested fruit nobody asked for went into the basket; Count is how many of it are there now.</summary>
    public readonly struct FruitBasketed
    {
        public readonly int FromPlot;
        public readonly string FruitId;
        public readonly int Count;
        public readonly bool IsGolden;

        public FruitBasketed(int fromPlot, string fruitId, int count, bool isGolden = false)
        {
            FromPlot = fromPlot;
            FruitId = fruitId;
            Count = count;
            IsGolden = isGolden;
        }
    }
}
