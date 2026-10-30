namespace RadiantOrchard.Gameplay
{
    /// <summary>Where a visitor is in their visit (CLAUDE.md 5.4). The bench (W2·05) adds its own phases.</summary>
    public enum VisitorPhase
    {
        /// <summary>Walking from the jungle to their waiting spot (BalanceConfig.VisitorWalkInSeconds).</summary>
        WalkingIn = 0,
        /// <summary>At their spot with a request; patience counts down.</summary>
        Waiting = 1,
        /// <summary>Healed, walking to the exit; their spot frees when they get there.</summary>
        Leaving = 2,
    }
}
