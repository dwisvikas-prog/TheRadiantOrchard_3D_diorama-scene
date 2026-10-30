using System.IO;
using RadiantOrchard.App;
using RadiantOrchard.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the placeholder tonic: a soft glowing dot sprite (until the tonic bottle art exists), the pooled
    /// Tonic prefab with its trail, and the "Tonics" object (TonicFlights) in the Game scene, wired into the
    /// GameInstaller. Re-running rebuilds all three.
    /// </summary>
    public static class TonicBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string PlaceholderFolder = "Assets/_Project/Art/Sprites/Placeholders";
        private const string DotPath = PlaceholderFolder + "/ph_soft_dot.png";
        private const string PrefabFolder = "Assets/_Project/Prefabs/VFX";
        private const string PrefabPath = PrefabFolder + "/Tonic.prefab";
        // URP's unlit sprite material: tonics glow at full brightness whatever the 2D lighting does.
        private const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string RootName = "Tonics";
        // Above every farm object (plots, landmarks, timers).
        private const int TonicOrder = 500;

        [MenuItem("Radiant Orchard/Setup/Build Tonic")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("TonicBuilder: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            EnsureFolder(PlaceholderFolder);
            EnsureFolder(PrefabFolder);
            EnsureDotSprite();
            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            // Load assets after opening the scene: opening a scene unloads unused assets.
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(DotPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            if (dot == null || material == null)
            {
                Debug.LogError($"TonicBuilder: missing {DotPath} or {UnlitSpriteMaterialPath}.");
                return;
            }

            GameObject source = BuildTonic(dot, material);
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath).GetComponent<TonicView>();
            Object.DestroyImmediate(source);

            GameInstaller installer = null;
            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (root.name == RootName) Object.DestroyImmediate(root);
                else if (installer == null) installer = root.GetComponentInChildren<GameInstaller>(true);
            }
            if (installer == null)
            {
                Debug.LogError("TonicBuilder: no GameInstaller in Game; run Radiant Orchard > Setup > Add Bootstrapper and GameInstaller first.");
                return;
            }

            var tonicsGo = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(tonicsGo, game);
            var flights = tonicsGo.AddComponent<TonicFlights>();
            var flightsSo = new SerializedObject(flights);
            flightsSo.FindProperty("_prefab").objectReferenceValue = prefab;
            flightsSo.ApplyModifiedPropertiesWithoutUndo();

            var installerSo = new SerializedObject(installer);
            installerSo.FindProperty("_tonics").objectReferenceValue = flights;
            installerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log("TonicBuilder: Tonic prefab and Tonics (TonicFlights) built and wired into the GameInstaller.");
        }

        private static GameObject BuildTonic(Sprite dot, Material material)
        {
            var go = new GameObject("Tonic");
            go.transform.localScale = new Vector3(0.7f, 0.7f, 1f);

            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = dot;
            sprite.sharedMaterial = material;
            sprite.sortingOrder = TonicOrder;

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = material;
            trail.sortingOrder = TonicOrder - 1;
            trail.time = 0.3f;
            trail.minVertexDistance = 0.05f;
            trail.widthMultiplier = 0.3f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.numCapVertices = 4;
            trail.emitting = false;
            trail.autodestruct = false;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;

            var view = go.AddComponent<TonicView>();
            var so = new SerializedObject(view);
            so.FindProperty("_sprite").objectReferenceValue = sprite;
            so.FindProperty("_trail").objectReferenceValue = trail;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // A white disc with a soft edge, tinted per fruit at runtime.
        private static void EnsureDotSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(DotPath) != null) return;

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01((1f - d) / 0.35f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            }
            File.WriteAllBytes(DotPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(DotPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(DotPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
