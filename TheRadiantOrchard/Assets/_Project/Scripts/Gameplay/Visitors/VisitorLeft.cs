namespace RadiantOrchard.Gameplay
{
    /// <summary>A healed visitor reached the exit. Spot is the path spot they freed, or -1 when they left from the bench.</summary>
    public readonly struct VisitorLeft
    {
        public readonly int VisitorId;
        public readonly int Spot;

        public VisitorLeft(int visitorId, int spot)
        {
            VisitorId = visitorId;
            Spot = spot;
        }
    }
}
