using System;
using System.Globalization;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// The game's only source of "now" for anything that must survive closing the app (never Time.time).
    /// Times are UTC; saved timestamps are Unix milliseconds. A dev offset lets the dev panel skip time to test
    /// week-long progression; it lives in memory only, so a restart returns to real time.
    /// </summary>
    public sealed class TimeService
    {
        public const string LocalDateFormat = "yyyy-MM-dd";

        private readonly IClock _clock;
        private TimeSpan _devOffset = TimeSpan.Zero;

        public TimeService(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public DateTime UtcNow => DateTime.SpecifyKind(_clock.UtcNow, DateTimeKind.Utc) + _devOffset;
        public long NowUtcMs => ToUnixMs(UtcNow);

        /// <summary>Today's date in the device's time zone, yyyy-MM-dd (dev offset included).</summary>
        public string LocalDate =>
            TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _clock.LocalTimeZone).ToString(LocalDateFormat, CultureInfo.InvariantCulture);

        public TimeSpan DevOffset => _devOffset;

        /// <summary>Raised whenever the dev offset changes, so dev builds can keep it across restarts.</summary>
        public event Action<TimeSpan> DevOffsetChanged;

        /// <summary>Dev panel only: move time forward (or back) to test growth, patience and daily resets.</summary>
        public void AddDevOffset(TimeSpan amount) => SetDevOffset(_devOffset + amount);

        public void ResetDevOffset() => SetDevOffset(TimeSpan.Zero);

        public void SetDevOffset(TimeSpan offset)
        {
            if (offset == _devOffset) return;
            _devOffset = offset;
            DevOffsetChanged?.Invoke(_devOffset);
        }

        /// <summary>
        /// Time passed since a saved Unix-ms timestamp. Zero if it was never set (0), and zero if it lies in the future
        /// because the device clock was turned back: no catch-up, no other punishment (CLAUDE.md section 7).
        /// </summary>
        public TimeSpan ElapsedSince(long utcMs)
        {
            if (utcMs <= 0) return TimeSpan.Zero;

            long diff = NowUtcMs - utcMs;
            if (diff < 0)
            {
                Log.Warn($"TimeService: saved time is {-diff / 1000} s in the future (clock turned back); counting no time passed.");
                return TimeSpan.Zero;
            }
            return TimeSpan.FromMilliseconds(diff);
        }

        /// <summary>True if the saved timestamp is later than now (the device clock was turned back).</summary>
        public bool IsInFuture(long utcMs) => utcMs > 0 && utcMs > NowUtcMs;

        /// <summary>
        /// True once the local date has moved past <paramref name="lastLocalDate"/> (or it is empty).
        /// Turning the clock back to an earlier day never counts as a new day.
        /// </summary>
        public bool IsNewLocalDay(string lastLocalDate)
        {
            if (string.IsNullOrEmpty(lastLocalDate)) return true;
            // yyyy-MM-dd sorts the same as the dates it stands for.
            return string.CompareOrdinal(LocalDate, lastLocalDate) > 0;
        }

        public static long ToUnixMs(DateTime utc) =>
            new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

        public static DateTime FromUnixMs(long utcMs) => DateTimeOffset.FromUnixTimeMilliseconds(utcMs).UtcDateTime;
    }
}
