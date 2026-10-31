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
    /// Mini-events (CLAUDE.md 5.11, ENGINEERING_GUIDE 9.1): one every 75 heals, picked by weight, never the last one
    /// twice unless nothing else can run, only events that can do something now; each effect does its job.
    /// </summary>
    public class MiniEventTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;
        private GameState _state;
        private EventBus _events;
        private BoostService _boosts;
        private HealService _heal;
        private PlotManager _plots;
        private VisitorManager _visitors;
        private MiniEventManager _manager;
        private List<MiniEventStarted> _started;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _state = GameState.CreateNew(_config.Balance);
            var time = new TimeService(new FakeClock());
            _events = new EventBus();
            _started = new List<MiniEventStarted>();
            _events.Subscribe<MiniEventStarted>(_started.Add);
            _boosts = new BoostService(_events);
            _heal = new HealService(_state, _config, _events, () => { }, null, _boosts);
            _plots = new PlotManager(_state, _config, time, _events, () => { });
            _visitors = new VisitorManager(_state, _config, time, _events, _heal, () => { }, new System.Random(3));
        }

        [TearDown]
        public void TearDown() => _manager?.Dispose();

        private void NewManager(params IMiniEventEffect[] effects) =>
            _manager = new MiniEventManager(_state, _config, _events, () => { }, new System.Random(5),
                effects.Length > 0 ? effects : new IMiniEventEffect[] { new GoldenHourEffect(_boosts), new VisitorRushEffect(_visitors), new BlessingRainEffect(_plots) });

        private MiniEventDefinition Event(string id) => _manager.Find(id);

        [Test]
        public void TheThreeEventsAreValidData()
        {
            Assert.AreEqual(75, _config.Balance.MiniEventEveryHeals, "CLAUDE.md 5.11");
            Assert.AreEqual(3, _config.MiniEvents.Count);
            var kinds = new HashSet<MiniEventKind>();
            foreach (MiniEventDefinition e in _config.MiniEvents)
            {
                Assert.IsTrue(kinds.Add(e.Kind), $"{e.Id}: one asset per kind");
                Assert.Greater(e.Weight, 0f, e.Id);
                Assert.IsTrue(_config.Strings.Contains(e.TitleKey), e.TitleKey);
                Assert.IsTrue(_config.Strings.Contains(e.BodyKey), e.BodyKey);
            }
            NewManager();
            Assert.AreEqual(MiniEventKind.GoldenHour, Event("golden_hour").Kind);
            Assert.AreEqual(90f, Event("golden_hour").DurationSeconds);
            Assert.AreEqual(2f, Event("golden_hour").Multiplier);
            Assert.AreEqual(3, Event("visitor_rush").VisitorCount);
            Assert.AreEqual(MiniEventKind.BlessingRain, Event("blessing_rain").Kind);
        }

        [Test]
        public void AnEventStartsAtExactlySeventyFiveHealsAndTheCountStartsAgain()
        {
            NewManager();
            for (int i = 0; i < 74; i++) _heal.Heal("strawberry");
            Assert.AreEqual(0, _started.Count);

            _heal.Heal("strawberry");
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(0, _state.HealedSinceEvent);
            Assert.AreEqual(_started[0].Id, _manager.LastId);

            for (int i = 0; i < 74; i++) _heal.Heal("strawberry");
            Assert.AreEqual(1, _started.Count, "The next one waits for another 75.");
        }

        [Test]
        public void SelectionNeverRepeatsTheLastAndHonoursWhatCanRun()
        {
            var events = _config.MiniEvents;
            var random = new System.Random(11);
            string last = null;
            var seen = new HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                MiniEventDefinition picked = MiniEventRules.Pick(events, _ => true, last, random);
                Assert.AreNotEqual(last, picked.Id, "Never the same twice in a row.");
                seen.Add(picked.Id);
                last = picked.Id;
            }
            Assert.AreEqual(3, seen.Count, "All three come up.");

            MiniEventDefinition only = MiniEventRules.Pick(events, e => e.Id == "blessing_rain", "blessing_rain", random);
            Assert.AreEqual("blessing_rain", only.Id, "The last one again when it is the only one possible.");
            Assert.IsNull(MiniEventRules.Pick(events, _ => false, null, random), "Nothing possible: nothing picked.");
        }

        [Test]
        public void WhenNothingCanRunTheCountWaitsForTheNextHeal()
        {
            NewManager(new BlessingRainEffect(_plots));
            for (int i = 0; i < 75; i++) _heal.Heal("strawberry");
            Assert.AreEqual(0, _started.Count, "No tree growing: Blessing Rain has nothing to do.");
            Assert.AreEqual(75, _state.HealedSinceEvent, "The count waits.");

            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));
            _heal.Heal("strawberry");
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual("blessing_rain", _started[0].Id);
            Assert.AreEqual(0, _state.HealedSinceEvent);
        }

        [Test]
        public void GoldenHourDoublesSeedsForNinetySeconds()
        {
            NewManager();
            Assert.IsTrue(_manager.StartNow("golden_hour"));
            Assert.IsTrue(_boosts.IsActive(BoostService.GoldenHarvest), "Reuses the Golden Harvest boost.");
            Assert.AreEqual(90f, _boosts.SecondsLeft(BoostService.GoldenHarvest), 1e-3f);
            Assert.AreEqual(20, _heal.Heal("strawberry"));

            _boosts.Tick(30f);
            Assert.IsTrue(_manager.StartNow("golden_hour"));
            Assert.AreEqual(90f, _boosts.SecondsLeft(BoostService.GoldenHarvest), 1e-3f, "Restarts, never stacks.");
            Assert.AreEqual(2f, _boosts.SeedMultiplier, 1e-5f);
        }

        [Test]
        public void VisitorRushFillsTheFreeSpotsTogether()
        {
            NewManager();
            Assert.IsFalse(_manager.StartNow("visitor_rush"), "Nothing planted: nobody would come.");

            _state.Plots[0].FruitId = "strawberry";
            _visitors.SpawnNow();
            Assert.AreEqual(2, _visitors.FreeSpots);
            Assert.IsTrue(_manager.StartNow("visitor_rush"));
            Assert.AreEqual(0, _visitors.FreeSpots, "Every free spot taken.");
            Assert.AreEqual(3, _visitors.Count);
            Assert.AreEqual(VisitorPhase.WalkingIn, _visitors.GetAt(2).Phase);
            Assert.AreEqual(_config.Balance.PatienceSeconds, _visitors.GetAt(2).PatienceSeconds, "Normal patience.");
            Assert.IsFalse(_manager.StartNow("visitor_rush"), "Path full.");
        }

        [Test]
        public void VisitorRushNeverTakesTheSpotsADailyWaveIsWaitingFor()
        {
            NewManager();
            _state.Plots[0].FruitId = "strawberry";
            _visitors.HoldSpawning = true;
            Assert.IsFalse(_manager.CanStart(Event("visitor_rush")));
        }

        [Test]
        public void BlessingRainRipensEveryGrowingTree()
        {
            NewManager();
            Assert.IsFalse(_manager.StartNow("blessing_rain"), "No trees.");
            _state.Seeds = 1000;
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(1, "watermelon"));
            Assert.IsTrue(_manager.StartNow("blessing_rain"));
            Assert.IsTrue(_plots.Get(0).IsRipe);
            Assert.IsTrue(_plots.Get(1).IsRipe);
            Assert.IsFalse(_manager.StartNow("blessing_rain"), "Nothing left growing.");
        }
    }
}
