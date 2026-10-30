using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The Week 1 loop in the real Game scene (W1·21): plant → ripen → harvest → tonic → heal. Only the fruit the
    /// placeholder visitor asked for (strawberry) heals. Saves go to a temporary folder, never the real save.
    /// </summary>
    public class HealLoopTests
    {
        private const float TonicFlightAndPop = 0.8f + 0.15f + 0.3f;
        private string _saveDir;
        private AppServices _services;
        private PlotManager _plots;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardHealTest", Guid.NewGuid().ToString("N"));
            Bootstrapper.SaveDirectoryOverride = _saveDir;
            SceneManager.LoadScene("Boot");

            float waited = 0f;
            while ((SceneManager.GetActiveScene().name != "Game" || AppServices.Current == null) && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;

            _services = AppServices.Current;
            // Test code may search the scene; the no-Find rule is for runtime code.
            var installer = Object.FindFirstObjectByType<GameInstaller>();
            Assert.IsNotNull(_services, "Boot did not start.");
            Assert.IsNotNull(installer);
            _plots = installer.DevPlots;
            Assert.IsNotNull(_plots, "GameInstaller did not finish wiring (check the console for missing references).");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var bootstrapper in Object.FindObjectsByType<Bootstrapper>(FindObjectsSortMode.None))
                Object.Destroy(bootstrapper.gameObject);
            Bootstrapper.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDir)) Directory.Delete(_saveDir, true);
        }

        [UnityTest]
        public IEnumerator HarvestingAStrawberryHealsTheVisitor()
        {
            GameState state = _services.State;
            Assert.AreEqual(0, state.Seeds);
            Assert.AreEqual(0, state.HealedTotal);

            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));
            _plots.RipenNow(0);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(0, GestureType.DoubleTap));

            yield return new WaitForSeconds(TonicFlightAndPop);

            Assert.AreEqual(_services.Config.Balance.SeedsPerHeal, state.Seeds, "+10 seeds when the tonic lands.");
            Assert.AreEqual(1, state.HealedTotal, "+1 healed.");
            Assert.AreEqual(1, state.HealedSinceQuiz);
        }

        [UnityTest]
        public IEnumerator AFruitTheVisitorDidNotAskForHealsNobody()
        {
            GameState state = _services.State;
            state.Seeds = 100;
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(1, "pineapple"));
            int seedsAfterPlanting = state.Seeds;
            _plots.RipenNow(1);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(1, GestureType.DoubleTap));

            yield return new WaitForSeconds(TonicFlightAndPop);

            Assert.AreEqual(seedsAfterPlanting, state.Seeds, "Until the Week 2 basket, an unrequested tonic pays nothing.");
            Assert.AreEqual(0, state.HealedTotal);
        }
    }
}
