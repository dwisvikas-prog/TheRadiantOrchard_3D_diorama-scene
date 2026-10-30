namespace RadiantOrchard.Gameplay
{
    /// <summary>A fruit is on its way to a visitor as a tonic, from a tree (FromPlot) or the basket (DeliveryService.FromBasket).</summary>
    public readonly struct DeliveryStarted
    {
        public readonly int FromPlot;
        public readonly string FruitId;
        public readonly int VisitorId;
        public readonly bool IsGolden;

        public DeliveryStarted(int fromPlot, string fruitId, int visitorId, bool isGolden = false)
        {
            FromPlot = fromPlot;
            FruitId = fruitId;
            VisitorId = visitorId;
            IsGolden = isGolden;
        }
    }
}
