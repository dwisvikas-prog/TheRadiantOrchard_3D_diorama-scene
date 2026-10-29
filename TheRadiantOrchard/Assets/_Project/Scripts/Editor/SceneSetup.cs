using RadiantOrchard.App;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Adds the composition-root objects to the two scenes: Bootstrapper (with GameConfig) in Boot, GameInstaller in
    /// Game. Safe to run again; it only adds what is missing. Run from the menu or with -executeMethod in batch mode.
    /// </summary>
    public static class SceneSetup
    {
        private const string BootScenePath = "Assets/_Project/Scenes/Boot.unity";
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";

        [MenuItem("Radiant Orchard/Setup/Add Bootstrapper and GameInstaller")]
        public static void AddCompositionRoot()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("SceneSetup: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            Scene boot = EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);

            // Load the config after opening the scene: opening a scene unloads unused assets, which would empty it.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (config == null)
            {
                Debug.LogError($"SceneSetup: no GameConfig at {GameConfigPath}.");
                return;
            }

            var bootstrapper = FindInRoots<Bootstrapper>(boot) ?? new GameObject("Bootstrapper").AddComponent<Bootstrapper>();
            var serialized = new SerializedObject(bootstrapper);
            serialized.FindProperty("_config").objectReferenceValue = config;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (serialized.FindProperty("_config").objectReferenceValue == null)
            {
                Debug.LogError("SceneSetup: could not assign GameConfig to the Bootstrapper.");
                return;
            }
            EditorSceneManager.MarkSceneDirty(boot);
            EditorSceneManager.SaveScene(boot);

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            if (FindInRoots<GameInstaller>(game) == null)
                new GameObject("GameInstaller").AddComponent<GameInstaller>();
            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);

            Debug.Log("SceneSetup: Bootstrapper in Boot (GameConfig assigned) and GameInstaller in Game are in place.");
        }

        // Editor-only setup code, so searching the scene's root objects is fine here (never at runtime).
        private static T FindInRoots<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                var found = root.GetComponentInChildren<T>(true);
                if (found != null) return found;
            }
            return null;
        }
    }
}
