namespace RadiantOrchard.Gameplay
{
    /// <summary>A fruit on the blooming Wisdom Tree was picked (not ripe) or has grown back (ripe).</summary>
    public readonly struct BloomFruitChanged
    {
        public readonly string FruitId;
        public readonly bool IsRipe;

        public BloomFruitChanged(string fruitId, bool isRipe)
        {
            FruitId = fruitId;
            IsRipe = isRipe;
        }
    }
}
