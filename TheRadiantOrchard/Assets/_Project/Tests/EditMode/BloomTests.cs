using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// The Wisdom Tree bloom and maintenance mode (CLAUDE.md 5.9): blooms once at the final stage (silently for a save
    /// already there), every fruit picked by a tap goes the usual way (visitor or basket, never golden, kept on the tree
    /// when it has nowhere to go), regrows after an hour, clock turned back waits, and it all survives a save.
    /// </summary>
    public class BloomTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private const long Hour = 3600_000L;

        private GameConfig _config;
        private GameState _state;
        private FakeClock _clock;
        private TimeService _time;
        private EventBus _events;
        private PlotManager _plots;
        private VibrancyManager _vibrancy;
        private WisdomTreeBloom _bloom;
        private List<BloomStarted> _started;
        private List<BloomFruitChanged> _changed;
        private List<FruitHarvested> _harvested;
        private List<HarvestHeld> _held;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _state = GameState.CreateNew(_config.Balance);
            _clock = new FakeClock();
            _time = new TimeService(_clock);
            _events = new EventBus();
            _started = new List<BloomStarted>();
            _changed = new List<BloomFruitChanged>();
            _harvested = new List<FruitHarvested>();
            _held = new List<HarvestHeld>();
            _events.Subscribe<BloomStarted>(_started.Add);
            _events.Subscribe<BloomFruitChanged>(_changed.Add);
            _events.Subscribe<FruitHarvested>(_harvested.Add);
            _events.Subscribe<HarvestHeld>(_held.Add);
            _plots = new PlotManager(_state, _config, _time, _events, () => { });
            _vibrancy = new VibrancyManager(_state, _config, _events, _plots, () => { });
        }

        [TearDown]
        public void TearDown()
        {
            _bloom?.Dispose();
            _vibrancy?.Dispose();
        }

        private void NewBloom() => _bloom = new WisdomTreeBloom(_state, _config, _time, _events, () => { });

        private void Bloomed()
        {
            NewBloom();
            _vibrancy.SetStage(_vibrancy.LastStage);
            Assert.IsTrue(_bloom.IsActive);
            _changed.Clear();
        }

        private void Advance(long ms) => _clock.UtcNow = _clock.UtcNow.AddMilliseconds(ms);

        [Test]
        public void SpecNumbersAndTextAreData()
        {
            Assert.AreEqual(3600, _config.Balance.BloomFruitRegrowSeconds, "CLAUDE.md 5.9: each regrows every 1 hr.");
            foreach (string key in new[] { "ui.bloom.title", "ui.bloom.continue", "tree.wisdom.name", "bloom.verse.text", "bloom.verse.reference" })
                Assert.IsTrue(_config.Strings.Contains(key), key);
            Assert.AreEqual("The Wisdom Tree is in bloom!",
                string.Format(_config.Strings.Get("ui.bloom.title"), _config.Strings.Get("tree.wisdom.name")), "The tree's name comes from data (\"the Vine\").");
        }

        [Test]
        public void RipeWhenNeverPickedOrAnHourLaterAndAClockTurnedBackWaits()
        {
            long now = 10 * Hour;
            Assert.IsTrue(BloomRules.IsRipe(0, now, 3600), "Never picked.");
            Assert.IsFalse(BloomRules.IsRipe(now, now, 3600));
            Assert.AreEqual(3600, BloomRules.SecondsUntilRipe(now, now + 1, 3600), "Partial seconds round up.");
            Assert.IsFalse(BloomRules.IsRipe(now, now + Hour - 1, 3600));
            Assert.AreEqual(1, BloomRules.SecondsUntilRipe(now, now + Hour - 1, 3600));
            Assert.IsTrue(BloomRules.IsRipe(now, now + Hour, 3600));
            Assert.AreEqual(0, BloomRules.SecondsUntilRipe(now, now + Hour, 3600));
            Assert.IsFalse(BloomRules.IsRipe(now, now - 5 * Hour, 3600), "Picked 'in the future': clock turned back.");
            Assert.AreEqual(3600, BloomRules.SecondsUntilRipe(now, now - 5 * Hour, 3600));
        }

        [Test]
        public void TheFinalStageBloomsTheTreeOnce()
        {
            NewBloom();
            _vibrancy.SetStage(_vibrancy.LastStage - 1);
            Assert.IsFalse(_bloom.IsActive);
            Assert.AreEqual(0, _started.Count);
            Assert.IsFalse(_bloom.IsRipe("strawberry"), "No fruit before the bloom.");
            Assert.AreEqual(HarvestJudgement.Ignore, _bloom.TryHarvest("strawberry"));

            _vibrancy.SetStage(_vibrancy.LastStage);
            Assert.IsTrue(_state.BloomActive, "Saved.");
            Assert.AreEqual(1, _started.Count);
            foreach (FruitDefinition fruit in _config.Fruits) Assert.IsTrue(_bloom.IsRipe(fruit.Id), $"{fruit.Id} hangs on the tree, ripe.");

            // A dev stage down and up again plays the final stage again, never the bloom.
            _vibrancy.SetStage(_vibrancy.LastStage - 1);
            _vibrancy.SetStage(_vibrancy.LastStage);
            Assert.AreEqual(1, _started.Count);
            Assert.IsTrue(_bloom.IsActive, "The bloom stays, like the trees.");
        }

        [Test]
        public void ASaveAlreadyAtTheFinalStageBloomsSilentlyAtLaunch()
        {
            _state.HealedTotal = _config.Balance.HealsToReach(_vibrancy.LastStage);
            _state.VibrancyStage = _vibrancy.LastStage;
            NewBloom();
            Assert.IsTrue(_bloom.IsActive);
            Assert.AreEqual(0, _started.Count, "No celebration at launch.");
        }

        [Test]
        public void ATapPicksARipeFruitWhichRegrowsAfterAnHour()
        {
            Bloomed();
            Assert.AreEqual(HarvestJudgement.Harvest, _bloom.TryHarvest("cherry"));
            Assert.AreEqual(1, _harvested.Count);
            Assert.AreEqual(WisdomTreeBloom.Tree, _harvested[0].Plot);
            Assert.AreEqual("cherry", _harvested[0].FruitId);
            Assert.IsFalse(_harvested[0].IsGolden, "Bloom fruit is never golden.");
            Assert.AreEqual(_time.NowUtcMs, _state.GetBloomFruitLastHarvest("cherry"), "Saved in bloomFruitLastHarvestUtc.");
            Assert.AreEqual(1, _changed.Count);
            Assert.IsFalse(_changed[0].IsRipe);
            Assert.IsFalse(_bloom.IsRipe("cherry"));
            Assert.IsTrue(_bloom.IsRipe("banana"), "Only that fruit was picked.");
            Assert.AreEqual(HarvestJudgement.Ignore, _bloom.TryHarvest("cherry"), "Nothing to pick until it grows back.");
            Assert.AreEqual(1, _harvested.Count);

            Advance(Hour - 1000);
            _bloom.Tick();
            Assert.AreEqual(1, _changed.Count, "Not yet.");
            Assert.AreEqual(1, _bloom.SecondsUntilRipe("cherry"));

            Advance(1000);
            _bloom.Tick();
            Assert.AreEqual(2, _changed.Count);
            Assert.AreEqual("cherry", _changed[1].FruitId);
            Assert.IsTrue(_changed[1].IsRipe, "Grown back after an hour.");
            Assert.AreEqual(HarvestJudgement.Harvest, _bloom.TryHarvest("cherry"));
            _bloom.Tick();
            Assert.AreEqual(3, _changed.Count, "Picked again; nothing else changed.");
        }

        [Test]
        public void AClockTurnedBackKeepsAPickedFruitWaiting()
        {
            Bloomed();
            _bloom.TryHarvest("apple");
            Advance(-5 * Hour);
            _bloom.Tick();
            Assert.IsFalse(_bloom.IsRipe("apple"));
            Advance(5 * Hour + Hour);
            _bloom.Tick();
            Assert.IsTrue(_bloom.IsRipe("apple"));
        }

        [Test]
        public void PickedFruitGoesToAWaitingVisitorOrTheBasketAndStaysWhenItHasNowhereToGo()
        {
            var boosts = new BoostService(_events);
            var heal = new HealService(_state, _config, _events, () => { }, _time, boosts);
            var visitors = new VisitorManager(_state, _config, _time, _events, heal, () => { }, new System.Random(3));
            var delivery = new DeliveryService(_state, _config, _events, visitors, () => { });
            var started = new List<DeliveryStarted>();
            _events.Subscribe<DeliveryStarted>(started.Add);
            try
            {
                Bloomed();
                _bloom.HarvestGate = delivery.CanTake;

                // A visitor asking for strawberry gets the one from the tree.
                _state.Plots[0].FruitId = "strawberry";
                _state.Plots[0].PlantedUtc = _time.NowUtcMs;
                visitors.SpawnNow();
                visitors.Tick(_config.Balance.VisitorWalkInSeconds + 0.1f);
                Assert.AreEqual(HarvestJudgement.Harvest, _bloom.TryHarvest("strawberry"));
                Assert.AreEqual(1, started.Count);
                Assert.AreEqual(WisdomTreeBloom.Tree, started[0].FromPlot);

                // Nobody wants grapes: the basket takes three, then they stay on the tree.
                for (int i = 0; i < _config.Balance.BasketMaxPerFruit; i++)
                {
                    _state.SetBloomFruitLastHarvest("grapes", 0);
                    Assert.AreEqual(HarvestJudgement.Harvest, _bloom.TryHarvest("grapes"));
                }
                Assert.AreEqual(_config.Balance.BasketMaxPerFruit, _state.GetBasketCount("grapes"));
                _state.SetBloomFruitLastHarvest("grapes", 0);
                Assert.AreEqual(HarvestJudgement.Held, _bloom.TryHarvest("grapes"));
                Assert.AreEqual(1, _held.Count);
                Assert.AreEqual(WisdomTreeBloom.Tree, _held[0].Plot);
                Assert.IsTrue(_bloom.IsRipe("grapes"), "Still on the tree, ready when someone asks.");
            }
            finally
            {
                delivery.Dispose();
            }
        }

        [Test]
        public void TheBloomAndItsPickedFruitsSurviveASave()
        {
            Bloomed();
            _bloom.TryHarvest("peach");
            _bloom.Dispose();

            GameState loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(_state));
            _bloom = new WisdomTreeBloom(loaded, _config, _time, _events, () => { });
            Assert.IsTrue(_bloom.IsActive);
            Assert.IsFalse(_bloom.IsRipe("peach"));
            Assert.IsTrue(_bloom.IsRipe("lemon"));
            Advance(Hour);
            Assert.IsTrue(_bloom.IsRipe("peach"));
        }

        [Test]
        public void DevRipenAllRegrowsEveryPickedFruit()
        {
            NewBloom();
            Assert.AreEqual(0, _bloom.RipenAll(), "Nothing before the bloom.");
            _bloom.Dispose();
            Bloomed();
            _bloom.TryHarvest("lemon");
            _bloom.TryHarvest("banana");
            Assert.AreEqual(2, _bloom.RipenAll());
            Assert.IsTrue(_bloom.IsRipe("lemon"));
            Assert.IsTrue(_bloom.IsRipe("banana"));
        }
    }
}
