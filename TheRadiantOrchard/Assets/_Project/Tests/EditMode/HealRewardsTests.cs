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
    /// Heal rewards (CLAUDE.md 5.6, 5.8, 5.12, 5.13): base 10 / bench 5, golden fruit ×3, Golden Harvest ×2, combo ×N up
    /// to ×5, all multiplied; the combo window; boosts; the golden fruit roll; golden fruit through harvest and basket.
    /// </summary>
    public class HealRewardsTests
    {
        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;
        private BalanceConfig _b;
        private GameState _state;
        private EventBus _events;
        private FakeClock _clock;
        private TimeService _time;
        private BoostService _boosts;
        private HealService _heal;
        private List<VisitorHealed> _healed;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _b = _config.Balance;
            _state = GameState.CreateNew(_b);
            _events = new EventBus();
            _clock = new FakeClock();
            _time = new TimeService(_clock);
            _boosts = new BoostService(_events);
            _heal = new HealService(_state, _config, _events, () => { }, _time, _boosts);
            _healed = new List<VisitorHealed>();
            _events.Subscribe<VisitorHealed>(_healed.Add);
        }

        private void Wait(double seconds) => _clock.UtcNow = _clock.UtcNow.AddSeconds(seconds);

        [TestCase(false, false, 1f, 1, 10, TestName = "Normal heal pays 10")]
        [TestCase(true, false, 1f, 1, 5, TestName = "Bench heal pays 5")]
        [TestCase(false, true, 1f, 1, 30, TestName = "Golden fruit pays 30")]
        [TestCase(true, true, 1f, 1, 15, TestName = "Golden fruit on the bench pays 15")]
        [TestCase(false, false, 2f, 1, 20, TestName = "Golden Harvest pays 20")]
        [TestCase(false, false, 1f, 2, 20, TestName = "Combo x2 pays 20")]
        [TestCase(false, false, 1f, 3, 30, TestName = "Combo x3 pays 30")]
        [TestCase(false, false, 1f, 9, 50, TestName = "Combo is capped at x5")]
        [TestCase(false, true, 2f, 2, 120, TestName = "Golden, Golden Harvest and combo x2 multiply to 120")]
        [TestCase(true, true, 2f, 1, 30, TestName = "Bench, golden and Golden Harvest multiply to 30")]
        public void SeedsTable(bool bench, bool golden, float boost, int combo, int expected)
        {
            Assert.AreEqual(expected, HealRules.SeedsFor(_b, bench, golden, boost, combo));
        }

        [Test]
        public void TheBalanceNumbersMatchTheSpec()
        {
            Assert.AreEqual(10, _b.SeedsPerHeal);
            Assert.AreEqual(5, _b.SeedsPerBenchHeal);
            Assert.AreEqual(3, _b.GoldenFruitSeedMultiplier);
            Assert.AreEqual(2, _b.GoldenHarvestSeedMultiplier);
            Assert.AreEqual(4f, _b.ComboWindowSeconds);
            Assert.AreEqual(5, _b.ComboMaxMultiplier);
            Assert.AreEqual(0.05f, _b.GoldenFruitChance, 1e-6f);
        }

        [Test]
        public void HealsWithinFourSecondsBuildACombo()
        {
            var combo = new ComboTracker(4f);
            Assert.AreEqual(1, combo.Register(0));
            Assert.AreEqual(2, combo.Register(3000));
            Assert.AreEqual(3, combo.Register(7000), "Exactly 4 s after the last one still counts.");
            Assert.AreEqual(3, combo.Current(9000));
            Assert.AreEqual(0, combo.Current(11001), "The window has passed.");
            Assert.AreEqual(1, combo.Register(11001), "A new combo starts.");
        }

        [Test]
        public void QuickVisitorHealsPayTheComboAndADevHealDoesNot()
        {
            Assert.AreEqual(10, _heal.Heal("strawberry", 1));
            Wait(2);
            Assert.AreEqual(20, _heal.Heal("strawberry", 2), "Second heal within 4 s: x2.");
            Wait(2);
            Assert.AreEqual(30, _heal.Heal("strawberry", 3));
            Assert.AreEqual(3, _healed[2].Combo);
            Assert.IsTrue(_healed[2].HasBonus);
            Wait(5);
            Assert.AreEqual(10, _heal.Heal("strawberry", 4), "Too slow: the combo starts again.");
            Assert.AreEqual(1, _healed[3].Combo);
            Assert.IsFalse(_healed[3].HasBonus);

            Assert.AreEqual(10, _heal.Heal("strawberry"), "Dev heals (no visitor) never build a combo.");
            Assert.AreEqual(10, _heal.Heal("strawberry"));
        }

        [Test]
        public void GoldenHarvestDoublesSeedsUntilItRunsOut()
        {
            _boosts.Start(BoostService.GoldenHarvest, _b.GoldenHarvestSeedMultiplier, _b.GoldenHarvestSeconds);
            Assert.AreEqual(20, _heal.Heal("strawberry"), "CLAUDE.md 5.6: 20 during Golden Harvest.");
            Assert.AreEqual(10, _heal.Heal("strawberry", fromBench: true));

            var ended = new List<BoostEnded>();
            _events.Subscribe<BoostEnded>(ended.Add);
            _boosts.Tick(_b.GoldenHarvestSeconds - 1f);
            Assert.IsTrue(_boosts.IsActive(BoostService.GoldenHarvest));
            _boosts.Tick(1f);
            Assert.IsFalse(_boosts.IsActive(BoostService.GoldenHarvest));
            Assert.AreEqual(1, ended.Count);
            Assert.AreEqual(10, _heal.Heal("strawberry"));
        }

        [Test]
        public void StartingABoostAgainRestartsItInsteadOfStacking()
        {
            _boosts.Start(BoostService.GoldenHarvest, 2f, 60f);
            _boosts.Tick(50f);
            _boosts.Start(BoostService.GoldenHarvest, 2f, 60f);
            Assert.AreEqual(2f, _boosts.SeedMultiplier);
            Assert.AreEqual(60f, _boosts.SecondsLeft(BoostService.GoldenHarvest));
            _boosts.Start("other", 1.5f, 10f);
            Assert.AreEqual(3f, _boosts.SeedMultiplier, 1e-5f, "Different boosts multiply.");
        }

        [Test]
        public void AGoldenTonicPaysTripleAndIsAnnounced()
        {
            Assert.AreEqual(30, _heal.Heal("strawberry", 1, golden: true));
            Assert.IsTrue(_healed[0].Golden);
            Assert.AreEqual(10, _healed[0].BaseSeeds);
        }

        [Test]
        public void TheGoldenRollIsFixedPerFruitAndAboutOneInTwenty()
        {
            Assert.AreEqual(GoldenFruitRules.IsGolden(2, 1_793_400_000_000, 0.05f), GoldenFruitRules.IsGolden(2, 1_793_400_000_000, 0.05f));
            Assert.IsFalse(GoldenFruitRules.IsGolden(0, 1_793_400_000_000, 0f));
            Assert.IsTrue(GoldenFruitRules.IsGolden(0, 1_793_400_000_000, 1f));
            Assert.IsFalse(GoldenFruitRules.IsGolden(0, 0, 1f), "Nothing planted, nothing golden.");

            int golden = 0;
            const int Cycles = 20000;
            long start = 1_793_400_000_000;
            for (int i = 0; i < Cycles; i++)
                if (GoldenFruitRules.IsGolden(i % 9, start + i * 300_123L, _b.GoldenFruitChance)) golden++;
            Assert.AreEqual(0.05, golden / (double)Cycles, 0.01, "About 1 in 20 harvests (CLAUDE.md 5.12).");
        }

        [Test]
        public void AGoldenFruitIsHarvestedGoldenAndTheNextOneIsRolledAgain()
        {
            var plots = new PlotManager(_state, _config, _time, _events, () => { });
            var harvested = new List<FruitHarvested>();
            _events.Subscribe<FruitHarvested>(harvested.Add);
            plots.TryPlant(0, "strawberry");
            Assert.IsTrue(plots.ForceGolden(0));
            plots.RipenNow(0);
            Assert.IsTrue(plots.Get(0).IsGolden, "Seen on the tree before picking.");

            Assert.IsTrue(plots.TryHarvest(0, out _));
            Assert.IsTrue(harvested[0].IsGolden);
            Wait(1);
            plots.RipenNow(0);
            long cycle = GrowthCalculator.CycleStartUtc(_state.Plots[0]);
            Assert.AreEqual(GoldenFruitRules.IsGolden(0, cycle, _b.GoldenFruitChance), plots.Get(0).IsGolden,
                "The dev override is used up; the next fruit follows the roll.");
            Assert.IsFalse(plots.Get(1).IsGolden, "Nothing planted there.");
        }

        [Test]
        public void AGoldenFruitStaysGoldenInTheBasketAndLeavesItFirst()
        {
            var visitors = new VisitorManager(_state, _config, _time, _events, _heal, () => { }, new System.Random(3));
            var delivery = new DeliveryService(_state, _config, _events, visitors, () => { });
            var started = new List<DeliveryStarted>();
            _events.Subscribe<DeliveryStarted>(started.Add);

            _events.Publish(new FruitHarvested(0, "strawberry"));
            _events.Publish(new FruitHarvested(0, "strawberry", isGolden: true));
            Assert.AreEqual(2, _state.GetBasketCount("strawberry"));
            Assert.AreEqual(1, _state.GetBasketGoldenCount("strawberry"));

            GameState loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(_state));
            Assert.AreEqual(1, loaded.GetBasketGoldenCount("strawberry"), "Saved.");

            _state.Plots[0].FruitId = "strawberry";
            Assert.AreEqual(1, visitors.ArriveAfterAway(TimeSpan.FromSeconds(_b.VisitorSpawnIntervalSeconds)));
            delivery.Tick();
            Assert.IsTrue(started[0].IsGolden, "The golden one goes first.");
            Assert.AreEqual(0, _state.GetBasketGoldenCount("strawberry"));
            Assert.AreEqual(30, delivery.Complete(started[0].VisitorId, "strawberry", golden: true));
            delivery.Dispose();
        }

        [Test]
        public void AnOlderSaveWithoutGoldenCountsLoadsWithNone()
        {
            var state = JsonUtility.FromJson<GameState>("{\"_version\":1,\"_basket\":[{\"_fruitId\":\"lemon\",\"_count\":2}]}");
            Assert.AreEqual(2, state.GetBasketCount("lemon"));
            Assert.AreEqual(0, state.GetBasketGoldenCount("lemon"));
        }
    }
}
