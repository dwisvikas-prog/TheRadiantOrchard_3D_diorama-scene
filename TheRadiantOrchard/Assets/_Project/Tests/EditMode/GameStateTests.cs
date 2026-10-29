using System.Linq;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>GameState: first-launch values and a lossless JSON round trip (save schema v1).</summary>
    public class GameStateTests
    {
        private static BalanceConfig Balance()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            Assert.IsNotNull(config, "No GameConfig asset.");
            Assert.IsNotNull(config.Balance, "GameConfig has no BalanceConfig.");
            return config.Balance;
        }

        [Test]
        public void NewStateHasThreeOpenPlotsAndZeroSeeds()
        {
            var state = GameState.CreateNew(Balance());

            Assert.AreEqual(GameState.CurrentVersion, state.Version);
            Assert.AreEqual(1, state.Version);
            Assert.AreEqual(0, state.Seeds);
            Assert.AreEqual(9, state.Plots.Count);
            Assert.AreEqual(3, state.Plots.Count(p => p.Unlocked));
            Assert.IsTrue(state.Plots.Take(3).All(p => p.Unlocked), "The first three plots should be the open ones.");
        }

        [Test]
        public void NewStateStartsFresh()
        {
            var state = GameState.CreateNew(Balance());

            Assert.AreEqual(0, state.LastSaveUtc);
            Assert.AreEqual(0, state.HealedTotal);
            Assert.AreEqual(0, state.HealedSinceQuiz);
            Assert.AreEqual(0, state.HealedSinceEvent);
            Assert.AreEqual(0, state.VibrancyStage);
            Assert.IsTrue(state.Plots.All(p => !p.IsPlanted && p.PlantedUtc == 0 && p.LastHarvestUtc == 0));
            Assert.AreEqual(0, state.Bench.Count);
            Assert.AreEqual(0, state.QuizSeen.Count);
            Assert.AreEqual(0, state.GetBasketCount("strawberry"));
            Assert.IsFalse(state.BloomActive);
            Assert.IsFalse(state.TutorialDone);
            Assert.AreEqual(string.Empty, state.WellText);
            Assert.AreEqual(string.Empty, state.LastDailyWaveDate);
            Assert.IsTrue(state.Settings.Sound);
            Assert.IsTrue(state.Settings.Music);
        }

        [Test]
        public void JsonRoundTripKeepsEveryField()
        {
            var state = GameState.CreateNew(Balance());
            state.LastSaveUtc = 1_761_700_000_000;
            state.Seeds = 1234;
            state.HealedTotal = 130;
            state.HealedSinceQuiz = 5;
            state.HealedSinceEvent = 55;
            state.VibrancyStage = 2;
            state.Plots[0].FruitId = "strawberry";
            state.Plots[0].PlantedUtc = 1_761_699_000_000;
            state.Plots[0].LastHarvestUtc = 1_761_699_500_000;
            state.Plots[4].Unlocked = true;
            state.SetBasketCount("lemon", 2);
            state.Bench.Add(new BenchVisitorState("pineapple", 1_761_699_900_000));
            state.QuizSeen.Add("peace");
            state.BloomActive = true;
            state.SetBloomFruitLastHarvest("cherry", 1_761_699_800_000);
            state.WellText = "Thank you for today ❤";
            state.TutorialDone = true;
            state.LastDailyWaveDate = "2026-10-29";
            state.Settings.Music = false;

            string json = JsonUtility.ToJson(state);
            var loaded = JsonUtility.FromJson<GameState>(json);

            Assert.AreEqual(1, loaded.Version);
            Assert.AreEqual(1_761_700_000_000, loaded.LastSaveUtc);
            Assert.AreEqual(1234, loaded.Seeds);
            Assert.AreEqual(130, loaded.HealedTotal);
            Assert.AreEqual(5, loaded.HealedSinceQuiz);
            Assert.AreEqual(55, loaded.HealedSinceEvent);
            Assert.AreEqual(2, loaded.VibrancyStage);
            Assert.AreEqual(9, loaded.Plots.Count);
            Assert.AreEqual("strawberry", loaded.Plots[0].FruitId);
            Assert.AreEqual(1_761_699_000_000, loaded.Plots[0].PlantedUtc);
            Assert.AreEqual(1_761_699_500_000, loaded.Plots[0].LastHarvestUtc);
            Assert.IsTrue(loaded.Plots[4].Unlocked);
            Assert.IsFalse(loaded.Plots[5].Unlocked);
            Assert.AreEqual(2, loaded.GetBasketCount("lemon"));
            Assert.AreEqual(1, loaded.Bench.Count);
            Assert.AreEqual("pineapple", loaded.Bench[0].FruitId);
            Assert.AreEqual(1_761_699_900_000, loaded.Bench[0].ArrivedUtc);
            CollectionAssert.AreEqual(new[] { "peace" }, loaded.QuizSeen);
            Assert.IsTrue(loaded.BloomActive);
            Assert.AreEqual(1_761_699_800_000, loaded.GetBloomFruitLastHarvest("cherry"));
            Assert.AreEqual("Thank you for today ❤", loaded.WellText);
            Assert.IsTrue(loaded.TutorialDone);
            Assert.AreEqual("2026-10-29", loaded.LastDailyWaveDate);
            Assert.IsTrue(loaded.Settings.Sound);
            Assert.IsFalse(loaded.Settings.Music);
        }

        [Test]
        public void BasketCountAddsUpdatesAndRemoves()
        {
            var state = GameState.CreateNew(Balance());
            state.SetBasketCount("apple", 1);
            state.SetBasketCount("apple", 3);
            Assert.AreEqual(3, state.GetBasketCount("apple"));
            state.SetBasketCount("apple", 0);
            Assert.AreEqual(0, state.GetBasketCount("apple"));
            StringAssert.DoesNotContain("apple", JsonUtility.ToJson(state), "An empty basket entry should be removed.");
        }

        [Test]
        public void BloomTimestampUpdatesInPlace()
        {
            var state = GameState.CreateNew(Balance());
            Assert.AreEqual(0, state.GetBloomFruitLastHarvest("joy"));
            state.SetBloomFruitLastHarvest("peach", 100);
            state.SetBloomFruitLastHarvest("peach", 200);
            Assert.AreEqual(200, state.GetBloomFruitLastHarvest("peach"));
        }

        [Test]
        public void SeedsNeverGoNegative()
        {
            var state = GameState.CreateNew(Balance());
            state.Seeds = 10;
            state.Seeds -= 25;
            Assert.AreEqual(0, state.Seeds);
        }
    }
}
