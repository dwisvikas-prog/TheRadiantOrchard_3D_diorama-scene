using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The Wisdom Tree bloom in the running game (CLAUDE.md 5.9): reaching the final stage plays the last zone and then
    /// the bloom celebration once (tree blossoms, verse card), picked fruit flies off the tree, and a saved bloom shows at
    /// launch with picked fruit growing back. Saves go to a temporary folder.
    /// </summary>
    public class BloomPlayTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardBloomTest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        private void WriteSave(GameState state) =>
            File.WriteAllText(Path.Combine(_saveDir, LocalFileStore.MainFileName), JsonUtility.ToJson(state));

        private static int IndexOf(GameConfig config, string fruitId)
        {
            for (int i = 0; i < config.Fruits.Count; i++)
                if (config.Fruits[i].Id == fruitId) return i;
            return -1;
        }

        [UnityTest]
        public IEnumerator TheFinalStagePlaysTheBloomOnceAndATreeFruitFliesToTheBasket()
        {
            // Two stages to go: the bloom must wait for the last zone, not follow the first one.
            WriteSave(new GameState { HealedTotal = 250, VibrancyStage = 3 });
            yield return TestGame.Boot(_saveDir);
            DevGame game = DevGame.Current;
            Assert.IsNotNull(game?.Bloom, "GameInstaller did not finish wiring.");
            // Test code may search the scene; the no-Find rule is for runtime code.
            var sequence = Object.FindFirstObjectByType<ZoneUnlockSequence>();
            var verse = Object.FindFirstObjectByType<VersePanelView>();
            WisdomTreeView tree = Object.FindFirstObjectByType<FarmView>().WisdomTreeView;
            Assert.IsFalse(tree.IsBlooming);

            StringAssert.Contains("blooms", DevActions.BloomNow(game));
            Assert.IsTrue(game.Bloom.IsActive);
            yield return null;
            Assert.IsFalse(tree.IsBlooming, "The bloom waits for its celebration.");

            for (float t = 0f; !verse.IsOpen && t < 30f; t += Time.deltaTime) yield return null;
            Assert.IsTrue(verse.IsOpen, "The verse card shows after the last zone and the bloom.");
            Assert.AreEqual(2, sequence.Played, "Zones 4 and 5 first.");
            var farm = Object.FindFirstObjectByType<FarmView>();
            Assert.AreEqual(1f, farm.ZoneColours[farm.ZoneColours.Count - 1].Saturation, 1e-3f, "The tree's zone is in colour before it blooms.");
            Assert.IsTrue(tree.IsBlooming);
            Assert.IsTrue(sequence.IsPlaying, "Input stays locked while the verse shows.");
            Assert.AreEqual("The Wisdom Tree is in bloom!", verse.Title);
            Assert.AreEqual("Galatians 5:22–23", verse.Reference);
            Assert.IsFalse(string.IsNullOrEmpty(verse.Verse));
            Assert.AreEqual(game.Config.Fruits.Count, tree.FruitCount, "One fruit per fruit of the Spirit.");
            for (int i = 0; i < tree.FruitCount; i++) Assert.IsTrue(tree.IsFruitRipe(i));

            verse.Hide();
            for (float t = 0f; sequence.IsPlaying && t < 5f; t += Time.deltaTime) yield return null;
            Assert.IsFalse(sequence.IsPlaying);
            Assert.AreEqual(1, sequence.BloomsPlayed);
            Assert.IsFalse(tree.IsRevealing);

            // Nobody is asking for cherry: it flies from the tree to the basket.
            Assert.AreEqual(HarvestJudgement.Harvest, game.Bloom.TryHarvest("cherry"));
            Assert.IsFalse(tree.IsFruitRipe(IndexOf(game.Config, "cherry")), "The picked fruit fades.");
            Assert.AreEqual(1, game.State.GetBasketCount("cherry"));
            for (float t = 0f; t < 1.5f; t += Time.deltaTime) yield return null;
            Assert.AreEqual(1, sequence.BloomsPlayed, "Played once.");
        }

        [UnityTest]
        public IEnumerator ASavedBloomShowsAtLaunchAndAPickedFruitGrowsBack()
        {
            var saved = new GameState { HealedTotal = 700, VibrancyStage = 5, BloomActive = true };
            // Picked a little under an hour ago in game time (the Editor may keep a dev time offset): it grows back a
            // couple of seconds after launch.
            TimeSpan offset = DevHooks.TimeOffsetStore?.Load() ?? TimeSpan.Zero;
            long gameNow = DateTimeOffset.UtcNow.Add(offset).ToUnixTimeMilliseconds();
            saved.SetBloomFruitLastHarvest("cherry", gameNow - 3598_000L);
            WriteSave(saved);
            yield return TestGame.Boot(_saveDir);
            DevGame game = DevGame.Current;
            Assert.IsNotNull(game?.Bloom, "GameInstaller did not finish wiring.");
            var sequence = Object.FindFirstObjectByType<ZoneUnlockSequence>();
            WisdomTreeView tree = Object.FindFirstObjectByType<FarmView>().WisdomTreeView;
            int cherry = IndexOf(game.Config, "cherry");

            Assert.IsTrue(tree.IsBlooming, "Blooming at launch, no celebration.");
            Assert.IsFalse(sequence.IsPlaying);
            Assert.IsFalse(tree.IsFruitRipe(cherry));
            Assert.IsTrue(tree.IsFruitRipe(IndexOf(game.Config, "strawberry")));

            for (float t = 0f; !tree.IsFruitRipe(cherry) && t < 6f; t += Time.deltaTime) yield return null;
            Assert.IsTrue(tree.IsFruitRipe(cherry), "Grown back after the hour.");
            Assert.IsTrue(game.Bloom.IsRipe("cherry"));
        }
    }
}
