namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Who gets a tonic (CLAUDE.md 5.5, no manual aiming): visitors waiting at their path spots first, longest waiting
    /// first; then visitors sitting on the bench, longest seated first. Only visitors who asked for that fruit and have
    /// no tonic coming already. Visitors still walking, leaving or off-screen are never targeted. Pure; reads snapshots.
    /// </summary>
    public static class DeliveryTargeting
    {
        public const int Nobody = -1;

        /// <summary>The visitor to send this fruit to, or Nobody.</summary>
        public static int Pick(VisitorManager visitors, string fruitId)
        {
            for (int i = 0; i < visitors.Count; i++)
            {
                VisitorInfo v = visitors.GetAt(i);
                if (v.Phase == VisitorPhase.Waiting && !v.Reserved && v.FruitId == fruitId) return v.Id;
            }
            for (int i = 0; i < visitors.BenchCount; i++)
            {
                VisitorInfo v = visitors.GetBenchAt(i);
                if (v.Phase == VisitorPhase.Sitting && !v.Reserved && v.FruitId == fruitId) return v.Id;
            }
            return Nobody;
        }
    }
}
