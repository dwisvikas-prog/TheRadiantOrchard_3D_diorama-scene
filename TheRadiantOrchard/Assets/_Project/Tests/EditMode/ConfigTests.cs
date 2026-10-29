using System.Linq;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Keeps BalanceConfig and GameConfig in step with CLAUDE.md sections 4 and 5.</summary>
    public class ConfigTests
    {
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";

        private static GameConfig LoadGameConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, $"No GameConfig at {GameConfigPath}.");
            return config;
        }

        private static BalanceConfig LoadBalance()
        {
            var balance = LoadGameConfig().Balance;
            Assert.IsNotNull(balance, "GameConfig has no BalanceConfig assigned.");
            return balance;
        }

        [Test]
        public void ThereIsExactlyOneBalanceConfig()
        {
            Assert.AreEqual(1, AssetDatabase.FindAssets("t:" + nameof(BalanceConfig)).Length);
        }

        [Test]
        public void GameConfigListsAllNineFruitsInSpecOrder()
        {
            var ids = LoadGameConfig().Fruits.Select(f => f != null ? f.Id : null).ToArray();
            CollectionAssert.AreEqual(
                new[] { "strawberry", "pineapple", "watermelon", "lemon", "grapes", "apple", "peach", "banana", "cherry" },
                ids);
        }

        [Test]
        public void FindFruitReturnsMatchOrNull()
        {
            var config = LoadGameConfig();
            Assert.AreEqual("lemon", config.FindFruit("lemon").Id);
            Assert.IsNull(config.FindFruit("orange"));
        }

        [Test]
        public void StartAndPlotsMatchSpec()
        {
            var b = LoadBalance();
            Assert.AreEqual(0, b.StartingSeeds);
            CollectionAssert.AreEqual(new[] { 3, 3, 5, 7, 9, 9 }, b.PlotsOpenAtStage.ToArray());
            Assert.AreEqual(9, b.TotalPlots);
            Assert.AreEqual(3, b.PlotsOpenAt(0));
            Assert.AreEqual(7, b.PlotsOpenAt(3));
        }

        [Test]
        public void VisitorsAndDeliveryMatchSpec()
        {
            var b = LoadBalance();
            Assert.AreEqual(30f, b.VisitorSpawnIntervalSeconds);
            Assert.AreEqual(3, b.MaxVisitorsOnPath);
            Assert.AreEqual(90f, b.PatienceSeconds);
            Assert.AreEqual(3, b.BenchSeats);
            Assert.AreEqual(3, b.MaxOfflineArrivals);
            Assert.AreEqual(3, b.BasketMaxPerFruit);
        }

        [Test]
        public void RewardsMatchSpec()
        {
            var b = LoadBalance();
            Assert.AreEqual(10, b.SeedsPerHeal);
            Assert.AreEqual(5, b.SeedsPerBenchHeal);
            Assert.AreEqual(20, b.SeedsPerHeal * b.GoldenHarvestSeedMultiplier, "Golden Harvest heal should pay 20.");
            Assert.AreEqual(0.05f, b.GoldenFruitChance, 1e-6f);
            Assert.AreEqual(3, b.GoldenFruitSeedMultiplier);
            Assert.AreEqual(4f, b.ComboWindowSeconds);
        }

        [Test]
        public void VibrancyStagesMatchSpec()
        {
            var b = LoadBalance();
            CollectionAssert.AreEqual(new[] { 40, 120, 250, 450, 700 }, b.StageThresholds.ToArray());
            Assert.AreEqual(40, b.HealsToReach(1));
            Assert.AreEqual(700, b.HealsToReach(5));
        }

        [Test]
        public void QuizEventsWaveBloomWellMatchSpec()
        {
            var b = LoadBalance();
            Assert.AreEqual(25, b.QuizEveryHeals);
            Assert.AreEqual(60f, b.GoldenHarvestSeconds);
            Assert.AreEqual(75, b.MiniEventEveryHeals);
            Assert.AreEqual(3, b.DailyWaveVisitors);
            Assert.AreEqual(120f, b.DailyWavePatienceSeconds);
            Assert.AreEqual(100, b.DailyWaveBonusSeeds);
            Assert.AreEqual(3600, b.BloomFruitRegrowSeconds);
            Assert.AreEqual(60, b.WellTextMaxLength);
        }

        [Test]
        public void LongPressIsInSpecRange()
        {
            float s = LoadBalance().LongPressSeconds;
            Assert.That(s, Is.InRange(0.5f, 0.6f));
        }

        [Test]
        public void TargetFrameRateIs60()
        {
            Assert.AreEqual(60, LoadBalance().TargetFrameRate);
        }

        [Test]
        public void AutosaveIntervalIsAFewSeconds()
        {
            Assert.That(LoadBalance().AutosaveMinIntervalSeconds, Is.InRange(1f, 10f));
        }

        [Test]
        public void FirstStageFruitsAreAffordableFromStart()
        {
            // The first plant must be possible with the starting seeds, or a new player is stuck.
            var config = LoadGameConfig();
            Assert.IsTrue(config.Fruits.Any(f => f.UnlockStage == 0 && f.PlantCost <= config.Balance.StartingSeeds));
        }
    }
}
