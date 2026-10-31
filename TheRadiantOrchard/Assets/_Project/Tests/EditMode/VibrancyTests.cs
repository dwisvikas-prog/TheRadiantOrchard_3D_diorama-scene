using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Vibrancy (CLAUDE.md 5.7, ENGINEERING_GUIDE 9.1): stage thresholds, meter progress, stage-ups opening plots and
    /// fruits in order, launch catch-up and the dev stage jump.
    /// </summary>
    public class VibrancyTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 31, 10, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private sealed class TelemetryLog : ITelemetrySink
        {
            public readonly List<string> Lines = new List<string>();
            public void Log(string eventName, string details) => Lines.Add(eventName + ":" + details);
        }

        private GameConfig _config;
        private BalanceConfig _balance;
        private GameState _state;
        private EventBus _events;
        private PlotManager _plots;
        private HealService _heal;
        private VibrancyManager _vibrancy;
        private List<StageReached> _reached;
        private List<VibrancyChanged> _changed;
        private List<PlotUnlocked> _unlocked;
        private TelemetryLog _telemetry;
        private ITelemetrySink _previousSink;
        private int _dirty;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _balance = _config.Balance;
            _telemetry = new TelemetryLog();
            _previousSink = Telemetry.Sink;
            Telemetry.Sink = _telemetry;
            Fresh();
        }

        // A new game with its own systems (the telemetry sink stays).
        private void Fresh()
        {
            _vibrancy?.Dispose();
            _vibrancy = null;
            _state = GameState.CreateNew(_balance);
            _events = new EventBus();
            _reached = new List<StageReached>();
            _changed = new List<VibrancyChanged>();
            _unlocked = new List<PlotUnlocked>();
            _events.Subscribe<StageReached>(_reached.Add);
            _events.Subscribe<VibrancyChanged>(_changed.Add);
            _events.Subscribe<PlotUnlocked>(_unlocked.Add);
            _dirty = 0;
            _plots = new PlotManager(_state, _config, new TimeService(new FakeClock()), _events, () => _dirty++);
            _heal = new HealService(_state, _config, _events, () => _dirty++);
        }

        [TearDown]
        public void TearDown()
        {
            _vibrancy?.Dispose();
            _vibrancy = null;
            Telemetry.Sink = _previousSink;
        }

        private void StartManager() => _vibrancy = new VibrancyManager(_state, _config, _events, _plots, () => _dirty++);

        private void HealTimes(int count)
        {
            for (int i = 0; i < count; i++) _heal.Heal("strawberry");
        }

        private int OpenPlots()
        {
            int open = 0;
            foreach (PlotState plot in _state.Plots) if (plot.Unlocked) open++;
            return open;
        }

        [Test]
        public void ThresholdsMatchTheSpec()
        {
            CollectionAssert.AreEqual(new[] { 40, 120, 250, 450, 700 }, _balance.StageThresholds);
            CollectionAssert.AreEqual(new[] { 3, 3, 5, 7, 9, 9 }, _balance.PlotsOpenAtStage);
        }

        [TestCase(0, 0)]
        [TestCase(39, 0)]
        [TestCase(40, 1)]
        [TestCase(119, 1)]
        [TestCase(120, 2)]
        [TestCase(250, 3)]
        [TestCase(449, 3)]
        [TestCase(450, 4)]
        [TestCase(699, 4)]
        [TestCase(700, 5)]
        [TestCase(5000, 5)]
        public void StageForHeals(int healed, int stage)
        {
            Assert.AreEqual(stage, VibrancyRules.StageFor(_balance, healed));
        }

        [Test]
        public void ProgressIsMeasuredInsideTheCurrentStage()
        {
            VibrancyProgress start = VibrancyRules.Progress(_balance, 10, 0);
            Assert.AreEqual(10, start.Done);
            Assert.AreEqual(40, start.Needed);
            Assert.AreEqual(0.25f, start.Fraction, 1e-5f);

            VibrancyProgress two = VibrancyRules.Progress(_balance, 57 + 40, 1);
            Assert.AreEqual(57, two.Done, "57 heals since stage 1 (40).");
            Assert.AreEqual(80, two.Needed, "Stage 1 → 2 is 40 → 120.");

            VibrancyProgress fresh = VibrancyRules.Progress(_balance, 120, 2);
            Assert.AreEqual(0, fresh.Done);
            Assert.AreEqual(130, fresh.Needed);
            Assert.AreEqual(0f, fresh.Fraction);

            VibrancyProgress done = VibrancyRules.Progress(_balance, 700, 5);
            Assert.IsTrue(done.IsComplete);
            Assert.AreEqual(1f, done.Fraction);
        }

        [Test]
        public void TheFortiethHealReachesStageOneOnce()
        {
            StartManager();
            HealTimes(39);
            Assert.AreEqual(0, _state.VibrancyStage);
            Assert.AreEqual(0, _reached.Count);

            HealTimes(1);
            Assert.AreEqual(1, _state.VibrancyStage);
            Assert.AreEqual(1, _reached.Count);
            Assert.AreEqual(1, _reached[0].Stage);
            Assert.IsFalse(_reached[0].IsFinal);
            Assert.AreEqual(3, OpenPlots(), "Stage 1 colours zone 1 only; no new plots.");
            CollectionAssert.Contains(_telemetry.Lines, "stage_reached:1");

            HealTimes(5);
            Assert.AreEqual(1, _reached.Count, "Announced once.");
            Assert.AreEqual(1 + 39 + 5, _changed.Count, "The meter hears every heal.");
            Assert.AreEqual(45, _changed[_changed.Count - 1].HealedTotal);
        }

        [Test]
        public void StageTwoOpensPlotsFourAndFiveAndLemonAndGrapes()
        {
            _state.Seeds = 10000;
            _state.HealedTotal = 119;
            _state.VibrancyStage = 1;
            StartManager();
            Assert.AreEqual(PlantResult.FruitLocked, _plots.TryPlant(0, "lemon"));

            HealTimes(1);
            Assert.AreEqual(2, _state.VibrancyStage);
            Assert.AreEqual(5, OpenPlots());
            CollectionAssert.AreEqual(new[] { 3, 4 }, _unlocked.ConvertAll(u => u.Plot));
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(3, "lemon"));
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(4, "grapes"));
            Assert.AreEqual(PlantResult.FruitLocked, _plots.TryPlant(0, "apple"), "Apple waits for stage 3.");
            Assert.Greater(_dirty, 0);
        }

        [Test]
        public void EveryStageOpensItsPlotsAndFruits()
        {
            int[] plots = { 3, 3, 5, 7, 9, 9 };
            for (int stage = 0; stage <= 5; stage++)
            {
                Fresh();
                _state.HealedTotal = stage == 0 ? 0 : _balance.HealsToReach(stage);
                StartManager();
                Assert.AreEqual(stage, _state.VibrancyStage);
                Assert.AreEqual(plots[stage], OpenPlots(), $"Stage {stage}");
                int lastOpen = plots[stage] - 1;
                foreach (FruitDefinition fruit in _config.Fruits)
                    Assert.AreEqual(fruit.UnlockStage <= stage, _plots.CanPlant(lastOpen, fruit) != PlantResult.FruitLocked,
                        $"{fruit.Id} at stage {stage}");
            }
        }

        [Test]
        public void CrossingTwoStagesAtOnceAnnouncesBothInOrder()
        {
            StartManager();
            _state.HealedTotal = 119;
            HealTimes(1);
            CollectionAssert.AreEqual(new[] { 1, 2 }, _reached.ConvertAll(r => r.Stage));
            Assert.AreEqual(2, _state.VibrancyStage);
            Assert.AreEqual(5, OpenPlots());
        }

        [Test]
        public void TheLastStageIsFinal()
        {
            _state.HealedTotal = 699;
            _state.VibrancyStage = 4;
            StartManager();
            HealTimes(1);
            Assert.AreEqual(5, _state.VibrancyStage);
            Assert.IsTrue(_reached[0].IsFinal);
            Assert.AreEqual(9, OpenPlots());
            Assert.IsTrue(_vibrancy.Progress.IsComplete);
        }

        [Test]
        public void HealsNeverLowerTheStage()
        {
            _state.VibrancyStage = 3;
            _state.HealedTotal = 10;
            StartManager();
            HealTimes(1);
            Assert.AreEqual(3, _state.VibrancyStage);
            Assert.AreEqual(7, OpenPlots(), "Launch opens the saved stage's plots.");
            Assert.AreEqual(0, _reached.Count);
        }

        [Test]
        public void ASaveAheadOfItsStageCatchesUpSilentlyAtLaunch()
        {
            _state.HealedTotal = 260;
            _state.VibrancyStage = 0;
            StartManager();
            Assert.AreEqual(3, _state.VibrancyStage);
            Assert.AreEqual(7, OpenPlots());
            Assert.AreEqual(0, _reached.Count, "No fanfare at launch.");
            Assert.Greater(_dirty, 0);
        }

        [Test]
        public void DevStageUpPlaysEachStageAndMovesTheHealCount()
        {
            StartManager();
            _vibrancy.SetStage(2);
            Assert.AreEqual(2, _state.VibrancyStage);
            Assert.AreEqual(120, _state.HealedTotal, "The meter matches the stage.");
            CollectionAssert.AreEqual(new[] { 1, 2 }, _reached.ConvertAll(r => r.Stage));
            Assert.AreEqual(5, OpenPlots());
            Assert.AreEqual(0f, _vibrancy.Progress.Fraction);
        }

        [Test]
        public void DevStageDownKeepsPlotsOpenAndIsClamped()
        {
            StartManager();
            _vibrancy.SetStage(99);
            Assert.AreEqual(5, _state.VibrancyStage);
            Assert.AreEqual(700, _state.HealedTotal);
            Assert.AreEqual(5, _reached.Count);
            _reached.Clear();

            _vibrancy.SetStage(1);
            Assert.AreEqual(1, _state.VibrancyStage);
            Assert.AreEqual(40, _state.HealedTotal);
            Assert.AreEqual(0, _reached.Count, "Going down announces nothing.");
            Assert.AreEqual(1, _changed[_changed.Count - 1].Stage, "Zones above stage 1 go grey.");
            Assert.AreEqual(9, OpenPlots(), "Trees are permanent; plots stay open.");

            _vibrancy.SetStage(-3);
            Assert.AreEqual(0, _state.VibrancyStage);
            Assert.AreEqual(0, _state.HealedTotal);
        }

        [Test]
        public void DisposeStopsListening()
        {
            StartManager();
            _vibrancy.Dispose();
            HealTimes(40);
            Assert.AreEqual(0, _state.VibrancyStage);
            _vibrancy = null;
        }
    }
}
