namespace RadiantOrchard.Gameplay
{
    /// <summary>A read-only snapshot of one visitor for views and delivery.</summary>
    public readonly struct VisitorInfo
    {
        public readonly int Id;
        public readonly string DefinitionId;
        public readonly string FruitId;
        /// <summary>StringTable key of what they said on arrival (their fruit's virtue), or null.</summary>
        public readonly string LineKey;
        /// <summary>Their path waiting spot, or -1 once they have left the path.</summary>
        public readonly int Spot;
        /// <summary>Their bench seat while walking to it or sitting, else -1.</summary>
        public readonly int Seat;
        public readonly VisitorPhase Phase;
        /// <summary>Seconds since the current phase began.</summary>
        public readonly float PhaseElapsed;
        /// <summary>1 = full patience, 0 = run out. Full while walking in.</summary>
        public readonly float Patience;
        /// <summary>How long their patience lasts on the path (the daily wave's visitors wait longer).</summary>
        public readonly float PatienceSeconds;
        public readonly bool PatienceRanOut;
        /// <summary>A tonic is on its way to this visitor.</summary>
        public readonly bool Reserved;

        public VisitorInfo(int id, string definitionId, string fruitId, string lineKey, int spot, int seat, VisitorPhase phase, float phaseElapsed, float patience, bool patienceRanOut, bool reserved, float patienceSeconds = 0f)
        {
            Id = id;
            DefinitionId = definitionId;
            FruitId = fruitId;
            LineKey = lineKey;
            Spot = spot;
            Seat = seat;
            Phase = phase;
            PhaseElapsed = phaseElapsed;
            Patience = patience;
            PatienceSeconds = patienceSeconds;
            PatienceRanOut = patienceRanOut;
            Reserved = reserved;
        }
    }
}
