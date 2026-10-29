using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>PlotManager rules: planting checks and costs, harvest and regrowth, ripening, unlocks, events.</summary>
    public class PlotManagerTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 29, 10, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;
        private GameState _state;
        private FakeClock _clock;
        private EventBus _events;
        private int _dirty;
        private PlotManager _plots;
        private List<PlotStageChanged> _stageEvents;
        private List<SeedsChanged> _seedEvents;
        private List<FruitHarvested> _harvests;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _state = GameState.CreateNew(_config.Balance);
            _clock = new FakeClock();
            _events = new EventBus();
            _dirty = 0;
            _stageEvents = new List<PlotStageChanged>();
            _seedEvents = new List<SeedsChanged>();
            _harvests = new List<FruitHarvested>();
            _events.Subscribe<PlotStageChanged>(_stageEvents.Add);
            _events.Subscribe<SeedsChanged>(_seedEvents.Add);
            _events.Subscribe<FruitHarvested>(_harvests.Add);
            _plots = new PlotManager(_state, _config, new TimeService(_clock), _events, () => _dirty++);
        }

        private void Advance(double seconds) => _clock.UtcNow = _clock.UtcNow.AddSeconds(seconds);

        [Test]
        public void StrawberryPlantsForFreeOnAnOpenPlot()
        {
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));
            Assert.AreEqual("strawberry", _state.Plots[0].FruitId);
            Assert.AreEqual(0, _state.Seeds);
            Assert.AreEqual(TreeStage.Sapling, _plots.Get(0).Stage);
            Assert.AreEqual(1, _stageEvents.Count);
            Assert.AreEqual(0, _seedEvents.Count, "A free plant does not change seeds.");
            Assert.Greater(_dirty, 0, "Planting must mark the save dirty.");
        }

        [Test]
        public void PlantingChargesSeeds()
        {
            _state.Seeds = 100;
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(1, "pineapple"));
            Assert.AreEqual(50, _state.Seeds);
            Assert.AreEqual(1, _seedEvents.Count);
            Assert.AreEqual(-50, _seedEvents[0].Delta);
        }

        [Test]
        public void PlantingRulesAreEnforced()
        {
            Assert.AreEqual(PlantResult.NotEnoughSeeds, _plots.TryPlant(0, "pineapple"), "0 seeds, pineapple costs 50.");
            Assert.AreEqual(PlantResult.FruitLocked, _plots.TryPlant(0, "lemon"), "Lemon unlocks at stage 2.");
            Assert.AreEqual(PlantResult.PlotLocked, _plots.TryPlant(5, "strawberry"), "Plot 6 is closed at the start.");
            Assert.AreEqual(PlantResult.UnknownFruit, _plots.TryPlant(0, "orange"));
            Assert.AreEqual(PlantResult.InvalidPlot, _plots.TryPlant(42, "strawberry"));

            _plots.TryPlant(0, "strawberry");
            Assert.AreEqual(PlantResult.PlotOccupied, _plots.TryPlant(0, "strawberry"), "Trees are permanent.");
            Assert.AreEqual(0, _state.Seeds);
        }

        [Test]
        public void HarvestOnlyWhenRipeThenRegrows()
        {
            _plots.TryPlant(0, "strawberry");
            Assert.IsFalse(_plots.TryHarvest(0, out _), "Not ripe yet.");

            Advance(300);
            _plots.Tick();
            Assert.AreEqual(TreeStage.Ripe, _plots.Get(0).Stage);
            Assert.IsTrue(_plots.TryHarvest(0, out FruitDefinition fruit));
            Assert.AreEqual("strawberry", fruit.Id);
            Assert.AreEqual(1, _harvests.Count);

            PlotInfo after = _plots.Get(0);
            Assert.AreEqual(TreeStage.Growing, after.Stage, "Regrows from full size.");
            Assert.AreEqual(300, after.SecondsUntilRipe, "Same timer again.");
            Assert.IsFalse(_plots.TryHarvest(0, out _), "One fruit per ripening.");
        }

        [Test]
        public void TickAnnouncesEachStageChangeOnce()
        {
            _plots.TryPlant(0, "strawberry");
            _stageEvents.Clear();

            Advance(10); _plots.Tick();
            Assert.AreEqual(0, _stageEvents.Count, "Still a sapling.");
            Advance(70); _plots.Tick();
            Assert.AreEqual(1, _stageEvents.Count);
            Assert.AreEqual(TreeStage.Growing, _stageEvents[0].Stage);
            Advance(300); _plots.Tick(); _plots.Tick();
            Assert.AreEqual(2, _stageEvents.Count);
            Assert.AreEqual(TreeStage.Ripe, _stageEvents[1].Stage);
        }

        [Test]
        public void TimeAwayIsCaughtUpFromTimestamps()
        {
            _plots.TryPlant(0, "strawberry");
            // A new manager over the same state, as after closing and reopening the app 2 hours later.
            Advance(2 * 3600);
            var reopened = new PlotManager(_state, _config, new TimeService(_clock), _events, () => { });
            Assert.AreEqual(TreeStage.Ripe, reopened.Get(0).Stage);
        }

        [Test]
        public void RipenNowAndRipenAll()
        {
            _state.Seeds = 1000;
            _plots.TryPlant(0, "strawberry");
            _plots.TryPlant(1, "watermelon");
            Assert.IsFalse(_plots.RipenNow(2), "Nothing planted on plot 3.");

            Assert.IsTrue(_plots.RipenNow(0));
            Assert.AreEqual(TreeStage.Ripe, _plots.Get(0).Stage);
            Assert.AreEqual(1, _plots.RipenAll(), "Only the watermelon still needed ripening.");
            Assert.AreEqual(TreeStage.Ripe, _plots.Get(1).Stage);

            _plots.TryHarvest(0, out _);
            Assert.IsTrue(_plots.RipenNow(0), "Ripening also works on a regrowing tree.");
            Assert.AreEqual(TreeStage.Ripe, _plots.Get(0).Stage);
        }

        [Test]
        public void StageUnlocksOpenTheRightPlots()
        {
            var unlocked = new List<PlotUnlocked>();
            _events.Subscribe<PlotUnlocked>(unlocked.Add);

            _state.VibrancyStage = 2;
            _plots.ApplyStageUnlocks();
            Assert.AreEqual(2, unlocked.Count, "Stage 2 opens plots 4 and 5.");
            Assert.IsTrue(_state.Plots[4].Unlocked);
            Assert.IsFalse(_state.Plots[5].Unlocked);

            _plots.ApplyStageUnlocks();
            Assert.AreEqual(2, unlocked.Count, "Nothing new to open.");
        }

        [Test]
        public void LockedPlotShowsEmpty()
        {
            Assert.IsFalse(_plots.Get(8).Unlocked);
            Assert.AreEqual(TreeStage.Empty, _plots.Get(8).Stage);
        }
    }
}
