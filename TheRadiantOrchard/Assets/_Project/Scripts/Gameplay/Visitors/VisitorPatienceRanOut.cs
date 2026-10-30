namespace RadiantOrchard.Gameplay
{
    /// <summary>A waiting visitor's patience ran out. The bench (W2·05) takes them from here; nobody is turned away.</summary>
    public readonly struct VisitorPatienceRanOut
    {
        public readonly int VisitorId;
        public readonly int Spot;

        public VisitorPatienceRanOut(int visitorId, int spot)
        {
            VisitorId = visitorId;
            Spot = spot;
        }
    }
}
