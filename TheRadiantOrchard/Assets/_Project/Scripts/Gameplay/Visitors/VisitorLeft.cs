namespace RadiantOrchard.Gameplay
{
    /// <summary>A healed visitor reached the exit; their spot is free again.</summary>
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
