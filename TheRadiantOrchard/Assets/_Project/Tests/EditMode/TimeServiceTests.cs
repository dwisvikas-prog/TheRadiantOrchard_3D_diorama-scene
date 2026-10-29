using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>TimeService with a fake clock: offline time, clock rollback, local midnight, dev offset.</summary>
    public class TimeServiceTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; }
            public TimeZoneInfo LocalTimeZone { get; set; } = TimeZoneInfo.Utc;
        }

        // India, UTC+5:30, no daylight saving: local midnight is 18:30 UTC.
        private static readonly TimeZoneInfo Ist =
            TimeZoneInfo.CreateCustomTimeZone("Test IST", new TimeSpan(5, 30, 0), "Test IST", "Test IST");

        private static DateTime Utc(int y, int mo, int d, int h = 0, int mi = 0, int s = 0) =>
            new DateTime(y, mo, d, h, mi, s, DateTimeKind.Utc);

        [Test]
        public void UnixConversionMatchesKnownDates()
        {
            Assert.AreEqual(0, TimeService.ToUnixMs(Utc(1970, 1, 1)));
            Assert.AreEqual(946_684_800_000, TimeService.ToUnixMs(Utc(2000, 1, 1)));
            Assert.AreEqual(Utc(2026, 10, 29, 6, 15, 30), TimeService.FromUnixMs(TimeService.ToUnixMs(Utc(2026, 10, 29, 6, 15, 30))));
        }

        [Test]
        public void NowFollowsTheClock()
        {
            var clock = new FakeClock { UtcNow = Utc(2026, 10, 29, 10, 0) };
            var time = new TimeService(clock);
            Assert.AreEqual(TimeService.ToUnixMs(clock.UtcNow), time.NowUtcMs);
            clock.UtcNow = clock.UtcNow.AddMinutes(5);
            Assert.AreEqual(TimeService.ToUnixMs(Utc(2026, 10, 29, 10, 5)), time.NowUtcMs);
        }

        [Test]
        public void ElapsedSinceCountsTimeAway()
        {
            var clock = new FakeClock { UtcNow = Utc(2026, 10, 29, 12, 0) };
            var time = new TimeService(clock);
            long savedTwoHoursAgo = TimeService.ToUnixMs(Utc(2026, 10, 29, 10, 0));
            Assert.AreEqual(TimeSpan.FromHours(2), time.ElapsedSince(savedTwoHoursAgo));
            Assert.IsFalse(time.IsInFuture(savedTwoHoursAgo));
        }

        [Test]
        public void NeverSavedMeansNoTimePassed()
        {
            var time = new TimeService(new FakeClock { UtcNow = Utc(2026, 10, 29) });
            Assert.AreEqual(TimeSpan.Zero, time.ElapsedSince(0));
            Assert.IsFalse(time.IsInFuture(0));
        }

        [Test]
        public void ClockTurnedBackCountsAsZero()
        {
            var time = new TimeService(new FakeClock { UtcNow = Utc(2026, 10, 29, 9, 0) });
            long savedInTheFuture = TimeService.ToUnixMs(Utc(2026, 10, 30, 9, 0));

            LogAssert.Expect(LogType.Warning, new Regex("clock turned back"));
            Assert.AreEqual(TimeSpan.Zero, time.ElapsedSince(savedInTheFuture));
            Assert.IsTrue(time.IsInFuture(savedInTheFuture));
        }

        [Test]
        public void DevOffsetMovesTimeAndResets()
        {
            var clock = new FakeClock { UtcNow = Utc(2026, 10, 29, 10, 0) };
            var time = new TimeService(clock);
            long savedNow = time.NowUtcMs;

            time.AddDevOffset(TimeSpan.FromHours(1));
            time.AddDevOffset(TimeSpan.FromDays(1));
            Assert.AreEqual(TimeSpan.FromHours(25), time.DevOffset);
            Assert.AreEqual(TimeSpan.FromHours(25), time.ElapsedSince(savedNow));

            time.ResetDevOffset();
            Assert.AreEqual(TimeSpan.Zero, time.ElapsedSince(savedNow));
        }

        [Test]
        public void DevOffsetChangesAreAnnounced()
        {
            var time = new TimeService(new FakeClock { UtcNow = Utc(2026, 10, 29) });
            var seen = new System.Collections.Generic.List<TimeSpan>();
            time.DevOffsetChanged += seen.Add;

            time.SetDevOffset(TimeSpan.FromDays(2));
            time.AddDevOffset(TimeSpan.FromHours(1));
            time.SetDevOffset(time.DevOffset); // no change, no event
            time.ResetDevOffset();

            CollectionAssert.AreEqual(new[] { TimeSpan.FromDays(2), TimeSpan.FromHours(49), TimeSpan.Zero }, seen);
        }

        [Test]
        public void LocalDateUsesTheDeviceTimeZone()
        {
            var clock = new FakeClock { LocalTimeZone = Ist, UtcNow = Utc(2026, 10, 29, 18, 29) };
            var time = new TimeService(clock);
            Assert.AreEqual("2026-10-29", time.LocalDate, "23:59 local is still the 29th.");

            clock.UtcNow = Utc(2026, 10, 29, 18, 31);
            Assert.AreEqual("2026-10-30", time.LocalDate, "00:01 local is the 30th.");
        }

        [Test]
        public void NewLocalDayOnlyWhenTheDateMovesForward()
        {
            var time = new TimeService(new FakeClock { LocalTimeZone = Ist, UtcNow = Utc(2026, 10, 29, 6, 0) });
            Assert.AreEqual("2026-10-29", time.LocalDate);

            Assert.IsTrue(time.IsNewLocalDay(""), "Never had a daily wave.");
            Assert.IsTrue(time.IsNewLocalDay("2026-10-28"), "Yesterday.");
            Assert.IsFalse(time.IsNewLocalDay("2026-10-29"), "Already today.");
            Assert.IsFalse(time.IsNewLocalDay("2026-10-30"), "Clock turned back a day: no new wave.");
        }

        [Test]
        public void DevOffsetCanCrossLocalMidnight()
        {
            var time = new TimeService(new FakeClock { LocalTimeZone = Ist, UtcNow = Utc(2026, 10, 29, 18, 0) });
            Assert.IsFalse(time.IsNewLocalDay("2026-10-29"));
            time.AddDevOffset(TimeSpan.FromHours(1));
            Assert.IsTrue(time.IsNewLocalDay("2026-10-29"), "+1 hr passes 00:00 local.");
        }
    }
}
