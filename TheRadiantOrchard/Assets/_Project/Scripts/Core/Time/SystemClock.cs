using System;

namespace RadiantOrchard.Core
{
    /// <summary>The device clock.</summary>
    public sealed class SystemClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
        public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Local;
    }
}
