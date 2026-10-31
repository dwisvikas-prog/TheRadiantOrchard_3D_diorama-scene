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
    /// The daily visitor wave (CLAUDE.md 5.10) with the rules agreed for W3·04: no wave on the first launch, once per
    /// local day, all path spots needed, waits for something planted, all members healed (bench counts) pays the bonus.
    /// </summary>
    public class DailyWaveTests
    {
        private const float Tick = 0.25f;

        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 11, 2, 9, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;
        private BalanceConfig _b;
        private GameState _state;
        private FakeClock _clock;
        private TimeService _time;
        private EventBus _events;
        private HealService _heal;
        private VisitorManager _visitors;
        private DailyWave _wave;
        private List<DailyWaveStarted> _started;
        private List<DailyWaveCompleted> _completed;

        private const string Today = "2026-11-02";
        private const string Yesterday = "2026-11-01";

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _b = _config.Balance;
            _state = GameState.CreateNew(_b);
            _clock = new FakeClock();
            _time = new TimeService(_clock);
            _events = new EventBus();
            _started = new List<DailyWaveStarted>();
            _completed = new List<DailyWaveCompleted>();
            _events.Subscribe<DailyWaveStarted>(_started.Add);
            _events.Subscribe<DailyWaveCompleted>(_completed.Add);
            _heal = new HealService(_state, _config, _events, () => { });
            _visitors = new VisitorManager(_state, _config, _time, _events, _heal, () => { }, new System.Random(7));
        }

        [TearDown]
        public void TearDown() => _wave?.Dispose();

        private void Launch(string lastWaveDate)
        {
            _state.LastDailyWaveDate = lastWaveDate;
            _wave = new DailyWave(_state, _config, _time, _events, _visitors, () => { });
            _wave.CheckNewDay();
        }

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Tick)
            {
                _visitors.Tick(Tick);
                _wave.Tick();
            }
        }

        private List<VisitorInfo> OnPath()
        {
            var list = new List<VisitorInfo>();
            for (int i = 0; i < _visitors.Count; i++)
                if (_visitors.GetAt(i).Spot >= 0) list.Add(_visitors.GetAt(i));
            return list;
        }

        private void HealAll(List<VisitorInfo> visitors)
        {
            foreach (VisitorInfo v in visitors) Assert.Greater(_visitors.Deliver(v.Id, v.FruitId), 0, $"Visitor {v.Id} healed.");
        }

        [Test]
        public void SpecNumbers()
        {
            Assert.AreEqual(3, _b.DailyWaveVisitors);
            Assert.AreEqual(120f, _b.DailyWavePatienceSeconds);
            Assert.AreEqual(100, _b.DailyWaveBonusSeeds);
        }

        [Test]
        public void TheFirstLaunchOnlyRecordsToday()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(string.Empty);
            Assert.AreEqual(Today, _state.LastDailyWaveDate);
            Assert.IsFalse(_wave.IsPending);
            Assert.AreEqual(0, _started.Count, "Day 1 belongs to the tutorial.");
        }

        [Test]
        public void ANewDayBringsThreeVisitorsTogetherWithTwoMinutesOfPatienceOnce()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(Yesterday);

            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(3, _started[0].Visitors);
            Assert.AreEqual(100, _started[0].BonusSeeds);
            List<VisitorInfo> wave = OnPath();
            Assert.AreEqual(3, wave.Count);
            foreach (VisitorInfo v in wave)
            {
                Assert.AreEqual(VisitorPhase.WalkingIn, v.Phase, "They walk in together.");
                Assert.AreEqual(120f, v.PatienceSeconds);
            }
            Assert.AreEqual(Today, _state.LastDailyWaveDate);
            Assert.AreEqual(1, wave.FindAll(v => v.LineKey != null).Count, "Only the first of them speaks.");

            Run(_b.VisitorWalkInSeconds + 100f);
            foreach (VisitorInfo v in OnPath()) Assert.IsFalse(v.PatienceRanOut, "Still waiting after 100 s (normal visitors give up at 90).");

            _wave.CheckNewDay();
            Assert.AreEqual(1, _started.Count, "Not again the same day.");
        }

        [Test]
        public void AClockTurnedBackBringsNoWave()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch("2026-11-05");
            Assert.AreEqual(0, _started.Count);
            Assert.IsFalse(_wave.IsPending);
        }

        [Test]
        public void TheDevTimeSkipAcrossMidnightBringsTheWave()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(Today);
            Assert.AreEqual(0, _started.Count);
            _time.SetDevOffset(TimeSpan.FromDays(1));
            _wave.CheckNewDay();
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual("2026-11-03", _state.LastDailyWaveDate);
        }

        [Test]
        public void ItWaitsForAnEmptyPathAndHoldsRegularVisitorsMeanwhile()
        {
            _state.Plots[0].FruitId = "strawberry";
            _state.LastDailyWaveDate = Today;
            _wave = new DailyWave(_state, _config, _time, _events, _visitors, () => { });
            Run(_b.VisitorWalkInSeconds + 1f);
            List<VisitorInfo> early = OnPath();
            Assert.AreEqual(1, early.Count, "A regular visitor is on the path.");

            _state.LastDailyWaveDate = Yesterday;
            _wave.CheckNewDay();
            Assert.IsTrue(_wave.IsPending, "Not all spots are free.");
            Assert.AreEqual(Yesterday, _state.LastDailyWaveDate, "The day is saved only when the wave comes.");
            Run(_b.VisitorSpawnIntervalSeconds * 2f);
            Assert.AreEqual(1, OnPath().Count, "No regular visitors while the wave waits.");

            HealAll(early);
            Run(_b.VisitorWalkOutSeconds + 0.5f);
            Assert.IsFalse(_wave.IsPending);
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(3, OnPath().Count);
        }

        [Test]
        public void ItWaitsForSomethingPlanted()
        {
            Launch(Yesterday);
            Assert.IsTrue(_wave.IsPending);
            Assert.AreEqual(Yesterday, _state.LastDailyWaveDate);
            Run(1f);
            Assert.AreEqual(0, _started.Count);

            _state.Plots[0].FruitId = "pineapple";
            Run(Tick);
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(Today, _state.LastDailyWaveDate);
        }

        [Test]
        public void HealingAllThreePaysTheBonusOnce()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(Yesterday);
            Run(_b.VisitorWalkInSeconds);
            List<VisitorInfo> wave = OnPath();

            HealAll(wave.GetRange(0, 2));
            Assert.AreEqual(0, _completed.Count, "Two of three: no bonus yet.");
            Assert.AreEqual(20, _state.Seeds);

            HealAll(wave.GetRange(2, 1));
            Assert.AreEqual(1, _completed.Count);
            Assert.AreEqual(100, _completed[0].BonusSeeds);
            Assert.AreEqual(wave[2].Id, _completed[0].LastVisitorId);
            Assert.AreEqual(30 + 100, _state.Seeds, "3 heals + the bonus.");
            Assert.IsFalse(_wave.IsActive);

            _heal.Heal("strawberry");
            Assert.AreEqual(1, _completed.Count, "Paid once.");
        }

        [Test]
        public void AMemberHealedOnTheBenchStillCounts()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(Yesterday);
            Run(_b.VisitorWalkInSeconds);
            List<VisitorInfo> wave = OnPath();
            HealAll(wave.GetRange(0, 2));

            _visitors.RunOutPatienceNow();
            Run(_b.VisitorWalkToBenchSeconds + Tick);
            Assert.IsTrue(_visitors.TryGet(wave[2].Id, out VisitorInfo seated));
            Assert.AreEqual(VisitorPhase.Sitting, seated.Phase);

            Assert.Greater(_visitors.Deliver(seated.Id, seated.FruitId), 0);
            Assert.AreEqual(1, _completed.Count, "No one is failed for running out of patience.");
        }

        [Test]
        public void DevStartNowIgnoresTheDate()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(Today);
            Assert.AreEqual(0, _started.Count);
            _wave.StartNow();
            Assert.AreEqual(1, _started.Count);
        }

        [Test]
        public void TheDateSurvivesASave()
        {
            _state.Plots[0].FruitId = "strawberry";
            Launch(Yesterday);
            GameState loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(_state));
            Assert.AreEqual(Today, loaded.LastDailyWaveDate);
        }
    }
}
