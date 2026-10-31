using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Dev panel actions (ENGINEERING_GUIDE 9.2) on real systems, and that the Dev assembly stays out of release builds.</summary>
    public class DevActionsTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private sealed class MemoryStore : ISaveStore
        {
            public string Json;
            public bool Deleted;
            public string Read() => Json;
            public string ReadBackup() => null;
            public void Write(string json) => Json = json;
            public void Delete() { Json = null; Deleted = true; }
        }

        private GameConfig _config;
        private MemoryStore _store;
        private SaveService _save;
        private DevGame _game;
        private List<SeedsChanged> _seedEvents;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            var time = new TimeService(new FakeClock());
            var events = new EventBus();
            _seedEvents = new List<SeedsChanged>();
            events.Subscribe<SeedsChanged>(_seedEvents.Add);
            _store = new MemoryStore();
            _save = new SaveService(_store, () => time.NowUtcMs, 3f);
            GameState state = _save.Load(_config.Balance);
            var plots = new PlotManager(state, _config, time, events, _save.MarkDirty);
            var heal = new HealService(state, _config, events, _save.MarkDirty);
            var visitors = new VisitorManager(state, _config, time, events, heal, _save.MarkDirty, new System.Random(1));
            var delivery = new DeliveryService(state, _config, events, visitors, _save.MarkDirty);
            var vibrancy = new VibrancyManager(state, _config, events, plots, _save.MarkDirty);
            _game = new DevGame(_config, state, time, events, _save, plots, heal, visitors, delivery, new BoostService(events), vibrancy);
        }

        [Test]
        public void SkippingAnHourRipensAStrawberry()
        {
            Assert.AreEqual(PlantResult.Planted, _game.Plots.TryPlant(0, "strawberry"));
            Assert.AreNotEqual(TreeStage.Ripe, _game.Plots.Get(0).Stage);

            DevActions.AddTime(_game, TimeSpan.FromHours(1));
            Assert.AreEqual(TreeStage.Ripe, _game.Plots.Get(0).Stage);
            Assert.AreEqual(TimeSpan.FromHours(1), _game.Time.DevOffset);

            DevActions.ResetTime(_game);
            Assert.AreEqual(TimeSpan.Zero, _game.Time.DevOffset);
        }

        [Test]
        public void AddingSeedsIsAnnouncedAndSaved()
        {
            _save.SaveNow();
            DevActions.AddSeeds(_game, 2000);
            Assert.AreEqual(2000, _game.State.Seeds);
            Assert.AreEqual(1, _seedEvents.Count);
            Assert.AreEqual(2000, _seedEvents[0].Delta);
            Assert.IsTrue(_save.IsDirty);
        }

        [Test]
        public void StageIsClampedAndOpensItsPlots()
        {
            DevActions.SetStage(_game, 99);
            Assert.AreEqual(_config.Balance.StageThresholds.Count, _game.State.VibrancyStage);
            for (int i = 0; i < _game.Plots.Count; i++) Assert.IsTrue(_game.State.Plots[i].Unlocked, $"Plot {i + 1}");

            Assert.AreEqual(700, _game.State.HealedTotal, "The heal count follows the stage.");

            DevActions.SetStage(_game, -3);
            Assert.AreEqual(0, _game.State.VibrancyStage);
            Assert.AreEqual(0, _game.State.HealedTotal);
        }

        [Test]
        public void HealingTenPaysAndCountsTen()
        {
            DevActions.Heal(_game, 10);
            Assert.AreEqual(10 * _config.Balance.SeedsPerHeal, _game.State.Seeds);
            Assert.AreEqual(10, _game.State.HealedTotal);
        }

        [Test]
        public void RipenAllRipensEveryPlantedTree()
        {
            _game.Plots.TryPlant(0, "strawberry");
            _game.Plots.TryPlant(1, "strawberry");
            DevActions.RipenAll(_game);
            Assert.AreEqual(TreeStage.Ripe, _game.Plots.Get(0).Stage);
            Assert.AreEqual(TreeStage.Ripe, _game.Plots.Get(1).Stage);
        }

        [Test]
        public void DeletingTheSaveStopsItBeingWrittenBack()
        {
            _save.SaveNow();
            Assert.IsNotNull(_store.Json);

            DevActions.DeleteSave(_game);
            Assert.IsTrue(_store.Deleted);
            Assert.IsTrue(_save.WritesBlocked);

            DevActions.AddSeeds(_game, 5);
            Assert.IsFalse(_save.SaveNow(), "A pause or quit must not write the old state back.");
            Assert.IsNull(_store.Json);
        }

        [Test]
        public void VisitorNowNeedsSomethingPlanted()
        {
            StringAssert.Contains("nothing planted", DevActions.SpawnVisitor(_game));
            _game.Plots.TryPlant(0, "strawberry");
            DevActions.SpawnVisitor(_game);
            Assert.AreEqual(1, _game.Visitors.Count);
            Assert.AreEqual("strawberry", _game.Visitors.GetAt(0).FruitId);
        }

        [Test]
        public void DumpSaveLogsTheJson()
        {
            LogAssert.Expect(LogType.Log, new System.Text.RegularExpressions.Regex("Save JSON"));
            DevActions.DumpSave(_game);
        }

        [Test]
        public void EveryActionIsSafeWithoutARunningGame()
        {
            Assert.AreEqual(DevActions.NotRunning, DevActions.AddTime(null, TimeSpan.FromHours(1)));
            Assert.AreEqual(DevActions.NotRunning, DevActions.AddSeeds(null, 1));
            Assert.AreEqual(DevActions.NotRunning, DevActions.SetStage(null, 1));
            Assert.AreEqual(DevActions.NotRunning, DevActions.RipenAll(null));
            Assert.AreEqual(DevActions.NotRunning, DevActions.Heal(null, 1));
            Assert.AreEqual(DevActions.NotRunning, DevActions.DeleteSave(null));
        }

        [Test]
        public void TheDevAssemblyIsOnlyBuiltIntoTheEditorAndDevelopmentBuilds()
        {
            string asmdef = File.ReadAllText("Assets/_Project/Scripts/Dev/RadiantOrchard.Dev.asmdef");
            StringAssert.Contains("\"DEVELOPMENT_BUILD || UNITY_EDITOR\"", asmdef, "The define constraint keeps the dev panel out of release builds.");
        }
    }
}
