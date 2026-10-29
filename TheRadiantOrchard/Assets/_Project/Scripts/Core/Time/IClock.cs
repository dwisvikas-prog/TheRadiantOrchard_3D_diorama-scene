using System;

namespace RadiantOrchard.Core
{
    /// <summary>The real time and the device's time zone. SystemClock in the game; a fake clock in tests.</summary>
    public interface IClock
    {
        DateTime UtcNow { get; }
        TimeZoneInfo LocalTimeZone { get; }
    }
}
