namespace RadiantOrchard.Gameplay
{
    /// <summary>What a gesture on a plot should do (HarvestRules).</summary>
    public enum HarvestJudgement
    {
        /// <summary>Nothing to harvest here, or the first half of the right gesture: no reaction.</summary>
        Ignore = 0,
        /// <summary>The fruit's own gesture on a ripe tree: pick it.</summary>
        Harvest = 1,
        /// <summary>A different gesture on a ripe tree: wobble and a soft sound, never a silent fail (CLAUDE.md 5.5).</summary>
        WrongGesture = 2,
    }
}
