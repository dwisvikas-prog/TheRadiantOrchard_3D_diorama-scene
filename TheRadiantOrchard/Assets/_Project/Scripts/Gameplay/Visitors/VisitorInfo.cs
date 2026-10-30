namespace RadiantOrchard.Gameplay
{
    /// <summary>A read-only snapshot of one visitor for views and delivery.</summary>
    public readonly struct VisitorInfo
    {
        public readonly int Id;
        public readonly string DefinitionId;
        public readonly string FruitId;
        public readonly int Spot;
        public readonly VisitorPhase Phase;
        /// <summary>Seconds since the current phase began.</summary>
        public readonly float PhaseElapsed;
        /// <summary>1 = full patience, 0 = run out. Full while walking in.</summary>
        public readonly float Patience;
        public readonly bool PatienceRanOut;
        /// <summary>A tonic is on its way to this visitor.</summary>
        public readonly bool Reserved;

        public VisitorInfo(int id, string definitionId, string fruitId, int spot, VisitorPhase phase, float phaseElapsed, float patience, bool patienceRanOut, bool reserved)
        {
            Id = id;
            DefinitionId = definitionId;
            FruitId = fruitId;
            Spot = spot;
            Phase = phase;
            PhaseElapsed = phaseElapsed;
            Patience = patience;
            PatienceRanOut = patienceRanOut;
            Reserved = reserved;
        }
    }
}
