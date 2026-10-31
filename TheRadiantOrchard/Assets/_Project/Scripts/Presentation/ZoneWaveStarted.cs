namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// The colour wave of a zone unlock has started (CLAUDE.md 5.7): the moment for the zone unlock music sting
    /// (SFX-17, played by the AudioService in Week 3).
    /// </summary>
    public readonly struct ZoneWaveStarted
    {
        public readonly int Stage;

        public ZoneWaveStarted(int stage)
        {
            Stage = stage;
        }
    }
}
