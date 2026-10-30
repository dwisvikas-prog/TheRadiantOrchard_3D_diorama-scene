namespace RadiantOrchard.Gameplay
{
    /// <summary>Where a visitor is in their visit (CLAUDE.md 5.4). Nobody is ever turned away.</summary>
    public enum VisitorPhase
    {
        /// <summary>Walking from the jungle to their waiting spot (BalanceConfig.VisitorWalkInSeconds).</summary>
        WalkingIn = 0,
        /// <summary>At their path spot with a request; patience counts down.</summary>
        Waiting = 1,
        /// <summary>Healed, walking to the exit; a path spot frees when they get there.</summary>
        Leaving = 2,
        /// <summary>Patience ran out; walking to a free bench seat (BalanceConfig.VisitorWalkToBenchSeconds).</summary>
        WalkingToBench = 3,
        /// <summary>On the bench, still asking; no patience timer. Saved in GameState.Bench.</summary>
        Sitting = 4,
        /// <summary>Patience ran out with the bench full; walking back into the jungle.</summary>
        WalkingAway = 5,
        /// <summary>Waiting in the jungle, out of sight, for a bench seat to free.</summary>
        Offscreen = 6,
    }
}
