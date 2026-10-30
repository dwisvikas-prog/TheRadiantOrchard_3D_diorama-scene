namespace RadiantOrchard.Gameplay
{
    /// <summary>A visitor sat down on the bench (the "bench sit" sound, CLAUDE.md 5.16).</summary>
    public readonly struct VisitorSatDown
    {
        public readonly int VisitorId;
        public readonly int Seat;

        public VisitorSatDown(int visitorId, int seat)
        {
            VisitorId = visitorId;
            Seat = seat;
        }
    }
}
