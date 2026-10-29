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
            Bootstrapper.SaveDirectoryOverride = _saveDir;
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
        public IEnumerator BootLoadsGameRunsTenSecondsAndSaves()
        {
            SceneManager.LoadScene("Boot");

            float waited = 0f;
            while (SceneManager.GetActiveScene().name != "Game" && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual("Game", SceneManager.GetActiveScene().name, "Boot did not hand over to Game.");
            Assert.IsNotNull(AppServices.Current, "Bootstrapper did not publish the services.");
            Assert.AreEqual(9, AppServices.Current.State.Plots.Count);
            Assert.AreEqual(60, Application.targetFrameRate);

            yield return new WaitForSecondsRealtime(10f);

            Assert.IsTrue(File.Exists(Path.Combine(_saveDir, "save.json")), "The autosave should have written save.json.");
        }
    }
}
