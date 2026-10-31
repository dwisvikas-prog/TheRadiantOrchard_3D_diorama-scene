namespace RadiantOrchard.Gameplay
{
    /// <summary>Where the Vibrancy Meter stands inside the current stage (CLAUDE.md 5.7).</summary>
    public readonly struct VibrancyProgress
    {
        /// <summary>Stage reached (0 = start, 5 = farm complete).</summary>
        public readonly int Stage;
        /// <summary>Heals made since the current stage was reached.</summary>
        public readonly int Done;
        /// <summary>Heals between the current stage and the next; 0 once the farm is complete.</summary>
        public readonly int Needed;
        public readonly bool IsComplete;

        public VibrancyProgress(int stage, int done, int needed, bool isComplete)
        {
            Stage = stage;
            Done = done;
            Needed = needed;
            IsComplete = isComplete;
        }

        /// <summary>How full the bar is, 0..1 (always 1 once complete).</summary>
        public float Fraction => IsComplete ? 1f : Needed > 0 ? (float)Done / Needed : 0f;
    }
}
