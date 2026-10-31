namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// A vibrancy stage was reached during play (CLAUDE.md 5.7): its zone turns to colour and its plots and fruits are
    /// open. Published once per stage, in order. Stage 5 = farm complete (the bloom follows in Week 3).
    /// </summary>
    public readonly struct StageReached
    {
        public readonly int Stage;
        public readonly bool IsFinal;

        public StageReached(int stage, bool isFinal)
        {
            Stage = stage;
            IsFinal = isFinal;
        }
    }
}
