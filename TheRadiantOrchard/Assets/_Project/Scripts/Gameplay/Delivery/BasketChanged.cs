namespace RadiantOrchard.Gameplay
{
    /// <summary>The basket now holds Count of this fruit (0 = none).</summary>
    public readonly struct BasketChanged
    {
        public readonly string FruitId;
        public readonly int Count;

        public BasketChanged(string fruitId, int count)
        {
            FruitId = fruitId;
            Count = count;
        }
    }
}
