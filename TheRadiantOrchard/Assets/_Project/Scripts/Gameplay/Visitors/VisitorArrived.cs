namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// A visitor came out of the jungle and is walking to their waiting spot, or (after time away) is already standing
    /// there.
    /// </summary>
    public readonly struct VisitorArrived
    {
        public readonly int VisitorId;
        public readonly string FruitId;
        public readonly int Spot;

        public VisitorArrived(int visitorId, string fruitId, int spot)
        {
            VisitorId = visitorId;
            FruitId = fruitId;
            Spot = spot;
        }
    }
}
