using System;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>The growth countdown text, and that it matches each fruit's grow time (CLAUDE.md §4).</summary>
    public class GrowthTimerTextTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 30, 9, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private static GameConfig Config() => AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");

        [TestCase(0, "0:00")]
        [TestCase(1, "0:01")]
        [TestCase(59, "0:59")]
        [TestCase(300, "5:00")]
        [TestCase(3599, "59:59")]
        [TestCase(3600, "1h 00m")]
        [TestCase(3660, "1h 01m")]
        [TestCase(7199, "1h 59m")]
        [TestCase(28800, "8h 00m")]
        [TestCase(-5, "0:00")]
        public void FormatsSecondsLeft(int seconds, string expected)
        {
            Assert.AreEqual(expected, GrowthTimerText.Format(seconds, Config().Strings));
        }

        [TestCase("strawberry", "5:00")]
        [TestCase("pineapple", "15:00")]
        [TestCase("watermelon", "30:00")]
        [TestCase("lemon", "1h 00m")]
        [TestCase("grapes", "2h 00m")]
        [TestCase("apple", "3h 00m")]
        [TestCase("peach", "4h 00m")]
        [TestCase("banana", "6h 00m")]
        [TestCase("cherry", "8h 00m")]
        public void FreshlyPlantedFruitShowsItsFullGrowTime(string fruitId, string expected)
        {
            var config = Config();
            var fruit = config.FindFruit(fruitId);
            Assert.IsNotNull(fruit, fruitId);
            Assert.AreEqual(expected, GrowthTimerText.Format(fruit.GrowSeconds, config.Strings));
        }

        [Test]
        public void CountdownFollowsTheClockAfterPlanting()
        {
            var config = Config();
            var clock = new FakeClock();
            var state = GameState.CreateNew(config.Balance);
            var plots = new PlotManager(state, config, new TimeService(clock), new EventBus(), () => { });

            Assert.AreEqual(PlantResult.Planted, plots.TryPlant(0, "strawberry"));
            Assert.AreEqual("5:00", GrowthTimerText.Format(plots.Get(0).SecondsUntilRipe, config.Strings));

            clock.UtcNow = clock.UtcNow.AddSeconds(61.5);
            Assert.AreEqual("3:59", GrowthTimerText.Format(plots.Get(0).SecondsUntilRipe, config.Strings), "Partial seconds round up.");

            clock.UtcNow = clock.UtcNow.AddSeconds(240);
            Assert.AreEqual(TreeStage.Ripe, plots.Get(0).Stage);
            Assert.AreEqual(0, plots.Get(0).SecondsUntilRipe);
        }
    }
}
