using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Harvest gestures (CLAUDE.md 5.5): the fruit's own gesture picks a ripe tree, anything else wobbles it.</summary>
    public class HarvestRulesTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 30, 10, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;

        [SetUp]
        public void SetUp() => _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");

        private static PlotInfo Plot(string fruitId, TreeStage stage) => new PlotInfo(0, true, fruitId, stage, 0, 1f);

        [TestCase("strawberry", GestureType.DoubleTap)]
        [TestCase("lemon", GestureType.LongPress)]
        [TestCase("peach", GestureType.Swipe)]
        public void TheFruitsOwnGestureHarvestsARipeTree(string fruitId, GestureType gesture)
        {
            Assert.AreEqual(HarvestJudgement.Harvest, HarvestRules.Judge(Plot(fruitId, TreeStage.Ripe), _config.FindFruit(fruitId), gesture));
        }

        [TestCase("strawberry", GestureType.LongPress)]
        [TestCase("strawberry", GestureType.Swipe)]
        [TestCase("lemon", GestureType.DoubleTap)]
        [TestCase("peach", GestureType.LongPress)]
        public void AnyOtherGestureOnARipeTreeIsWrong(string fruitId, GestureType gesture)
        {
            Assert.AreEqual(HarvestJudgement.WrongGesture, HarvestRules.Judge(Plot(fruitId, TreeStage.Ripe), _config.FindFruit(fruitId), gesture));
        }

        [TestCase(TreeStage.Empty)]
        [TestCase(TreeStage.Sapling)]
        [TestCase(TreeStage.Growing)]
        public void TreesThatAreNotRipeIgnoreEveryGesture(TreeStage stage)
        {
            FruitDefinition strawberry = _config.FindFruit("strawberry");
            foreach (GestureType gesture in Enum.GetValues(typeof(GestureType)))
                Assert.AreEqual(HarvestJudgement.Ignore, HarvestRules.Judge(Plot("strawberry", stage), strawberry, gesture), gesture.ToString());
            Assert.AreEqual(HarvestJudgement.Ignore, HarvestRules.JudgeTap(Plot("lemon", stage), _config.FindFruit("lemon")));
        }

        [Test]
        public void ASingleTapIsTheStartOfADoubleTapButWrongForOtherFruits()
        {
            Assert.AreEqual(HarvestJudgement.Ignore, HarvestRules.JudgeTap(Plot("strawberry", TreeStage.Ripe), _config.FindFruit("strawberry")));
            Assert.AreEqual(HarvestJudgement.WrongGesture, HarvestRules.JudgeTap(Plot("lemon", TreeStage.Ripe), _config.FindFruit("lemon")));
            Assert.AreEqual(HarvestJudgement.WrongGesture, HarvestRules.JudgeTap(Plot("peach", TreeStage.Ripe), _config.FindFruit("peach")));
        }

        [Test]
        public void MissingFruitIsIgnored()
        {
            Assert.AreEqual(HarvestJudgement.Ignore, HarvestRules.Judge(Plot("no_such_fruit", TreeStage.Ripe), null, GestureType.DoubleTap));
            Assert.AreEqual(HarvestJudgement.Ignore, HarvestRules.JudgeTap(Plot("no_such_fruit", TreeStage.Ripe), null));
        }

        [Test]
        public void PlotManagerHarvestsWithTheRightGestureAndWobblesOnTheWrongOne()
        {
            var clock = new FakeClock();
            var state = GameState.CreateNew(_config.Balance);
            state.VibrancyStage = 4;
            state.Seeds = 10000;
            var events = new EventBus();
            var rejected = new List<HarvestRejected>();
            var harvested = new List<FruitHarvested>();
            events.Subscribe<HarvestRejected>(rejected.Add);
            events.Subscribe<FruitHarvested>(harvested.Add);
            var plots = new PlotManager(state, _config, new TimeService(clock), events, () => { });

            Assert.AreEqual(PlantResult.Planted, plots.TryPlant(0, "strawberry"));
            Assert.AreEqual(PlantResult.Planted, plots.TryPlant(1, "lemon"));
            Assert.AreEqual(HarvestJudgement.Ignore, plots.HandleGesture(0, GestureType.DoubleTap), "Not ripe yet.");
            plots.RipenAll();

            Assert.AreEqual(HarvestJudgement.WrongGesture, plots.HandleGesture(0, GestureType.LongPress));
            Assert.AreEqual(1, rejected.Count);
            Assert.AreEqual(0, rejected[0].Plot);
            Assert.AreEqual("strawberry", rejected[0].FruitId);
            Assert.AreEqual(TreeStage.Ripe, plots.Get(0).Stage, "A wrong gesture never costs the fruit.");

            Assert.AreEqual(HarvestJudgement.Ignore, plots.HandleTap(0), "First tap of a double tap.");
            Assert.AreEqual(HarvestJudgement.WrongGesture, plots.HandleTap(1), "A tap on a ripe lemon hints at another gesture.");
            Assert.AreEqual(2, rejected.Count);

            Assert.AreEqual(HarvestJudgement.Harvest, plots.HandleGesture(0, GestureType.DoubleTap));
            Assert.AreEqual(1, harvested.Count);
            Assert.AreEqual(TreeStage.Growing, plots.Get(0).Stage, "Regrows on the same timer (5.3).");
            Assert.AreEqual(HarvestJudgement.Ignore, plots.HandleGesture(0, GestureType.DoubleTap), "Nothing left to pick.");

            Assert.AreEqual(HarvestJudgement.Harvest, plots.HandleGesture(1, GestureType.LongPress));
            Assert.AreEqual(HarvestJudgement.Ignore, plots.HandleGesture(-1, GestureType.Swipe));
            Assert.AreEqual(HarvestJudgement.Ignore, plots.HandleTap(99));
        }
    }
}
