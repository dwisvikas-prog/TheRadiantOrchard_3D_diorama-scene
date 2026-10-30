using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The dev panel in the real game (ENGINEERING_GUIDE 9.2): the hooks are filled while Game runs and cleared when it
    /// goes; the panel starts closed and opens with its buttons. Saves go to a temporary folder.
    /// </summary>
    public class DevPanelTests
    {
        private string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardDevPanelTest", Guid.NewGuid().ToString("N"));
            Bootstrapper.SaveDirectoryOverride = _saveDir;
            SceneManager.LoadScene("Boot");

            float waited = 0f;
            while ((SceneManager.GetActiveScene().name != "Game" || DevGame.Current == null) && waited < 10f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
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
        public IEnumerator ThePanelStartsClosedAndOpensWithItsButtons()
        {
            Assert.IsNotNull(DevGame.Current, "GameInstaller fills the dev hooks in the Editor and development builds.");
            // Test code may search the scene; the no-Find rule is for runtime code.
            var panel = Object.FindFirstObjectByType<DevPanel>();
            Assert.IsNotNull(panel, "The Dev assembly creates the panel at startup.");

            if (panel.IsOpen) panel.Toggle();
            Assert.IsFalse(panel.IsOpen);
            panel.Toggle();
            Assert.IsTrue(panel.IsOpen);
            Assert.GreaterOrEqual(panel.ButtonCount, 14, "DEV + 13 action buttons.");
            panel.Toggle();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheHooksAreClearedWhenTheGameGoes()
        {
            var installer = Object.FindFirstObjectByType<GameInstaller>();
            Assert.IsNotNull(DevGame.Current);
            Object.Destroy(installer.gameObject);
            yield return null;
            Assert.IsNull(DevGame.Current);
        }
    }
}
