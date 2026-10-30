using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The loop in the real Game scene: plant → ripen → harvest → tonic → heal, and fruit nobody asked for waiting in
    /// the basket until someone does (W2·07). Saves go to a temporary folder, never the real save.
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
            yield return TestGame.Boot(_saveDir);

            _services = AppServices.Current;
            Assert.IsNotNull(_services, "Boot did not start.");
            _plots = DevGame.Current?.Plots;
            Assert.IsNotNull(_plots, "GameInstaller did not finish wiring (check the console for missing references).");
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        [UnityTest]
        public IEnumerator AVisitorAsksForTheStrawberryAndIsHealedByIt()
        {
            GameState state = _services.State;
            VisitorManager visitors = DevGame.Current.Visitors;
            Assert.AreEqual(0, state.Seeds);
            Assert.AreEqual(0, visitors.Count, "No visitors before anything is planted.");

            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));

            // The first visitor comes right away and walks to their spot.
            float waited = 0f;
            while ((visitors.Count == 0 || visitors.GetAt(0).Phase != VisitorPhase.Waiting) && waited < 8f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(VisitorPhase.Waiting, visitors.GetAt(0).Phase, "A visitor arrived and waits at a spot.");
            Assert.AreEqual("strawberry", visitors.GetAt(0).FruitId, "They ask for the only fruit planted.");

            _plots.RipenNow(0);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(0, GestureType.DoubleTap));
            yield return new WaitForSeconds(TonicFlightAndPop);

            Assert.AreEqual(_services.Config.Balance.SeedsPerHeal, state.Seeds, "+10 seeds when the tonic lands.");
            Assert.AreEqual(1, state.HealedTotal, "+1 healed.");
            Assert.AreEqual(VisitorPhase.Leaving, visitors.GetAt(0).Phase, "They walk off once healed.");
        }

        [UnityTest]
        public IEnumerator AHarvestWithNobodyWaitingGoesIntoTheBasket()
        {
            GameState state = _services.State;
            state.Seeds = 100;
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(1, "pineapple"));
            int seedsAfterPlanting = state.Seeds;
            // Harvested in the same frame, before the first visitor has even appeared.
            _plots.RipenNow(1);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(1, GestureType.DoubleTap));

            yield return new WaitForSeconds(TonicFlightAndPop);

            Assert.AreEqual(seedsAfterPlanting, state.Seeds, "Nothing is paid until someone is healed.");
            Assert.AreEqual(0, state.HealedTotal);
            Assert.AreEqual(1, state.GetBasketCount("pineapple"), "It waits in the basket.");
            // Test code may search the scene; the no-Find rule is for runtime code.
            BasketChipView chip = Object.FindFirstObjectByType<BasketView>().ChipFor("pineapple");
            Assert.IsTrue(chip.gameObject.activeInHierarchy, "The HUD basket shows it.");
        }

        [UnityTest]
        public IEnumerator AFruitInTheBasketHealsTheNextVisitorWhoAsksForIt()
        {
            GameState state = _services.State;
            VisitorManager visitors = DevGame.Current.Visitors;
            Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));
            // Picked in the same frame, before the first visitor has even appeared.
            _plots.RipenNow(0);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(0, GestureType.DoubleTap));
            Assert.AreEqual(1, state.GetBasketCount("strawberry"));

            // The first visitor walks in, asks for a strawberry, and gets the one from the basket.
            float waited = 0f;
            while (state.HealedTotal == 0 && waited < 10f)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            Assert.AreEqual(1, state.HealedTotal, "Healed from the basket.");
            Assert.AreEqual(_services.Config.Balance.SeedsPerHeal, state.Seeds);
            Assert.AreEqual(0, state.GetBasketCount("strawberry"));
        }
    }
}
