using System.Collections;
using System.IO;
using RadiantOrchard.App;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// Starts and stops the real game for a PlayMode test, with the save in a temporary folder so the real save is
    /// never touched. Every fixture uses it, so each test gets a freshly booted game whatever ran before it.
    /// </summary>
    internal static class TestGame
    {
        private const float TimeoutSeconds = 10f;

        /// <summary>Boots from the Boot scene and waits until Game runs with its services (or the timeout passes).</summary>
        public static IEnumerator Boot(string saveDirectory)
        {
            Bootstrapper.SaveDirectoryOverride = saveDirectory;
            SceneManager.LoadScene("Boot");
            // The load happens next frame. Until then a Game scene left by the previous test still looks ready, and a
            // load still pending when that test's teardown clears the override would read the real save.
            yield return null;

            float waited = 0f;
            while ((SceneManager.GetActiveScene().name != "Game" || AppServices.Current == null) && waited < TimeoutSeconds)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
        }

        /// <summary>Ends the game's services and deletes the temporary save folder.</summary>
        public static void Shutdown(string saveDirectory)
        {
            // Test code may search the scene; the no-Find rule is for runtime code.
            foreach (var bootstrapper in Object.FindObjectsByType<Bootstrapper>(FindObjectsSortMode.None))
                Object.Destroy(bootstrapper.gameObject);
            Bootstrapper.SaveDirectoryOverride = null;
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
        }
    }
}
