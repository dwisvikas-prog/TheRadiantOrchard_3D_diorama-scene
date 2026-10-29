using System;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Published when the app comes back from the background, with the real time that passed (0 if the clock was
    /// turned back). The time away at launch is on AppServices.LaunchTimeAway, because no system is listening yet then.
    /// </summary>
    public readonly struct TimeAwayApplied
    {
        public readonly TimeSpan Elapsed;

        public TimeAwayApplied(TimeSpan elapsed)
        {
            Elapsed = elapsed;
        }
    }
}
