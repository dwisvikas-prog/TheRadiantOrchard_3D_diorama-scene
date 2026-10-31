using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// ENGINEERING_GUIDE 9.1 smoke test: Boot loads, Game runs 10 seconds without errors, and a save is written.
    /// Any error logged during the test fails it. Saves go to a temporary folder, never the real save.
    /// </summary>
    public class BootSmokeTests
    {
        private string _saveDir;

        [SetUp]
        public void SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardBootTest", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        [UnityTest]
        public IEnumerator BootLoadsGameRunsTenSecondsAndSaves()
        {
            yield return TestGame.Boot(_saveDir);

            Assert.AreEqual("Game", SceneManager.GetActiveScene().name, "Boot did not hand over to Game.");
            Assert.IsNotNull(AppServices.Current, "Bootstrapper did not publish the services.");
            Assert.AreEqual(9, AppServices.Current.State.Plots.Count);
            Assert.AreEqual(60, Application.targetFrameRate);

            yield return new WaitForSecondsRealtime(10f);

            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "save.json")), "The autosave should have written save.json.");
        }

        [UnityTest]
        public IEnumerator EveryZoneStartsGreyOnTheDesaturationMaterial()
        {
            yield return TestGame.Boot(_saveDir);
            // Test code may search the scene; the no-Find rule is for runtime code.
            var farm = Object.FindFirstObjectByType<RadiantOrchard.Presentation.FarmView>();
            Assert.AreEqual(5, farm.ZoneColours.Count, "CLAUDE.md 5.1: 5 zones.");
            for (int z = 0; z < farm.ZoneColours.Count; z++)
            {
                Assert.AreEqual(0f, farm.ZoneColours[z].Saturation, $"Zone {z + 1} is grey at the start.");
                Renderer renderer = farm.Zones[z].GetComponent<Renderer>();
                Assert.AreEqual("Radiant Orchard/Sprite Desaturate", renderer.sharedMaterial.shader.name, $"Zone {z + 1}");
            }
        }
    }
}
