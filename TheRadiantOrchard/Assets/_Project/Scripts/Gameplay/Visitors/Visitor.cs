using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>One visitor's live state, shared by VisitorManager and BenchSeating. Outside the assembly they are VisitorInfo snapshots.</summary>
    internal sealed class Visitor
    {
        public int Id;
        public string FruitId;
        public string LineKey;
        public int Spot = -1;
        public int Seat = -1;
        public VisitorPhase Phase;
        public float Elapsed;
        // Seconds of patience on the path; 0 = BalanceConfig.PatienceSeconds (the daily wave gives more).
        public float PatienceSeconds;
        public bool Reserved;
        public bool RanOut;
        // Their entry in GameState.Bench once they have sat down; removed when healed.
        public BenchVisitorState Saved;
    }
}
