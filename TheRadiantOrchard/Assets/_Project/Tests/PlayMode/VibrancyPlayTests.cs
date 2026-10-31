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
    /// Vibrancy in the running game (CLAUDE.md 5.7): a saved stage shows its zones, plots and meter at launch, and the
    /// dev stage jump colours zones, opens plots and moves the meter. Saves go to a temporary folder.
    /// </summary>
    public class VibrancyPlayTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardVibrancyTest", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        private static void AssertZones(FarmView farm, int stage)
        {
            for (int z = 0; z < farm.ZoneColours.Count; z++)
                Assert.AreEqual(z < stage ? 1f : 0f, farm.ZoneColours[z].Saturation, $"Zone {z + 1} at stage {stage}.");
        }

        private static int OpenPlots(GameState state)
        {
            int open = 0;
            foreach (PlotState plot in state.Plots) if (plot.Unlocked) open++;
            return open;
        }

        [UnityTest]
        public IEnumerator ASavedStageShowsItsZonesPlotsAndMeterAtLaunch()
        {
            Directory.CreateDirectory(_saveDir);
            var saved = new GameState { HealedTotal = 260, VibrancyStage = 3 };
            File.WriteAllText(Path.Combine(_saveDir, LocalFileStore.MainFileName), JsonUtility.ToJson(saved));

            yield return TestGame.Boot(_saveDir);
            Assert.IsNotNull(DevGame.Current?.Vibrancy, "GameInstaller did not finish wiring.");

            // Test code may search the scene; the no-Find rule is for runtime code.
            AssertZones(Object.FindFirstObjectByType<FarmView>(), 3);
            Assert.AreEqual(7, OpenPlots(AppServices.Current.State));
            var meter = Object.FindFirstObjectByType<VibrancyMeterView>();
            Assert.AreEqual("Stage 3", meter.StageText);
            Assert.AreEqual("10/200", meter.CountText, "250 → 450.");
            Assert.AreEqual(0.05f, meter.Fill, 1e-4f);
        }

        [UnityTest]
        public IEnumerator DevStageUpColoursZonesOpensPlotsAndMovesTheMeter()
        {
            yield return TestGame.Boot(_saveDir);
            DevGame game = DevGame.Current;
            Assert.IsNotNull(game?.Vibrancy, "GameInstaller did not finish wiring.");
            var farm = Object.FindFirstObjectByType<FarmView>();
            var meter = Object.FindFirstObjectByType<VibrancyMeterView>();
            AssertZones(farm, 0);
            Assert.AreEqual("Stage 0", meter.StageText);
            Assert.AreEqual("0/40", meter.CountText);

            DevActions.SetStage(game, 2);
            Assert.IsTrue(farm.ZoneColours[1].IsSweeping, "Zone 2 sweeps into colour.");
            yield return new WaitForSeconds(farm.ZoneSweepSeconds + 0.3f);

            AssertZones(farm, 2);
            Assert.AreEqual(5, OpenPlots(game.State));
            Assert.AreEqual(PlantResult.Planted, game.Plots.CanPlant(3, game.Config.FindFruit("strawberry")));
            Assert.AreNotEqual(PlantResult.FruitLocked, game.Plots.CanPlant(3, game.Config.FindFruit("lemon")));
            Assert.AreEqual("Stage 2", meter.StageText);
            Assert.AreEqual("0/130", meter.CountText);

            game.Heal.Heal("strawberry");
            Assert.AreEqual("1/130", meter.CountText, "The meter follows every heal.");

            DevActions.SetStage(game, 1);
            AssertZones(farm, 1);
            Assert.AreEqual(5, OpenPlots(game.State), "Plots stay open.");

            DevActions.SetStage(game, 5);
            yield return new WaitForSeconds(farm.ZoneSweepSeconds + 0.3f);
            AssertZones(farm, 5);
            Assert.AreEqual(9, OpenPlots(game.State));
            Assert.AreEqual("Farm restored", meter.CountText);
            Assert.AreEqual(1f, meter.Fill);
        }
    }
}
