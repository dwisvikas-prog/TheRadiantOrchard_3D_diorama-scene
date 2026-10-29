using NUnit.Framework;
using RadiantOrchard.Core;

namespace RadiantOrchard.Tests.EditMode
{
    public class GameClockTests
    {
        [Test]
        public void TicksOncePerInterval()
        {
            var clock = new GameClock(0.25f);
            int ticks = 0;
            clock.Ticked += () => ticks++;

            for (int i = 0; i < 4; i++) clock.Advance(0.25f);
            Assert.AreEqual(4, ticks, "Exact intervals.");

            // 1.1 s of 60 fps frames: 4 more ticks (a little slack for float rounding at the boundaries).
            for (int i = 0; i < 66; i++) clock.Advance(1f / 60f);
            Assert.AreEqual(8, ticks);
        }

        [Test]
        public void ALongHitchGivesOneTickNotABurst()
        {
            var clock = new GameClock(0.25f);
            int ticks = 0;
            clock.Ticked += () => ticks++;
            clock.Advance(5f);
            Assert.AreEqual(1, ticks);
        }

        [Test]
        public void ZeroOrNegativeTimeDoesNothing()
        {
            var clock = new GameClock(0.25f);
            int ticks = 0;
            clock.Ticked += () => ticks++;
            clock.Advance(0f);
            clock.Advance(-1f);
            Assert.AreEqual(0, ticks);
        }
    }
}
