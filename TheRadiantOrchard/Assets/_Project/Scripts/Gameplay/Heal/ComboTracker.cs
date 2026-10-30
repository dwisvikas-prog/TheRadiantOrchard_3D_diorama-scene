namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The heal combo (CLAUDE.md 5.13): a heal within the window of the previous heal adds one to the combo, otherwise
    /// it starts again at 1. Pure; the caller passes the time in milliseconds.
    /// </summary>
    public sealed class ComboTracker
    {
        private readonly long _windowMs;
        private long _lastHealMs = long.MinValue;
        private int _count;

        public ComboTracker(float windowSeconds) => _windowMs = (long)(windowSeconds * 1000f);

        /// <summary>Records a heal and returns the combo it makes (1 = no combo).</summary>
        public int Register(long nowMs)
        {
            _count = IsRunning(nowMs) ? _count + 1 : 1;
            _lastHealMs = nowMs;
            return _count;
        }

        /// <summary>The combo the next heal would continue, or 0 once the window has passed.</summary>
        public int Current(long nowMs) => IsRunning(nowMs) ? _count : 0;

        private bool IsRunning(long nowMs) =>
            _lastHealMs != long.MinValue && nowMs >= _lastHealMs && nowMs - _lastHealMs <= _windowMs;
    }
}
