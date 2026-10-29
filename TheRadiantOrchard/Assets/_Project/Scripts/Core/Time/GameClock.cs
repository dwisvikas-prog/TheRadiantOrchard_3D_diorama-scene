using System;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// The one central simulation tick (growth, patience...), a few times per second, instead of every object running
    /// its own Update. The Bootstrapper advances it with real (unscaled) frame time.
    /// </summary>
    public sealed class GameClock
    {
        private readonly float _intervalSeconds;
        private float _accumulated;

        public GameClock(float intervalSeconds)
        {
            _intervalSeconds = Math.Max(0.01f, intervalSeconds);
        }

        public event Action Ticked;

        public float IntervalSeconds => _intervalSeconds;

        /// <summary>Adds frame time; raises Ticked at most once per call so a long hitch never causes a burst of ticks.</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;
            _accumulated += deltaSeconds;
            if (_accumulated < _intervalSeconds) return;

            _accumulated %= _intervalSeconds;
            Ticked?.Invoke();
        }
    }
}
