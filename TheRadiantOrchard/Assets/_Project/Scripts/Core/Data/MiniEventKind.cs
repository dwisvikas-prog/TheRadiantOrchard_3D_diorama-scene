namespace RadiantOrchard.Core
{
    /// <summary>
    /// What a mini-event does (CLAUDE.md 5.11). Each kind has one effect class in Gameplay; a new kind (Phase 2's
    /// "challenge") is a new value, its effect and an asset.
    /// </summary>
    public enum MiniEventKind
    {
        GoldenHour = 0,
        VisitorRush = 1,
        BlessingRain = 2,
    }
}
