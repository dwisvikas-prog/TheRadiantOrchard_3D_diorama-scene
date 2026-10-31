using RadiantOrchard.App;
using RadiantOrchard.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the zone unlock sequence (CLAUDE.md 5.7, FX-11) in the Game scene: the petal particle prefab
    /// (Prefabs/VFX/ZoneWave, placeholder soft dots) and a "ZoneUnlock" object holding the ZoneUnlockSequence, wired
    /// into the GameInstaller. Run Build Tonic first (it makes the soft-dot sprite). Re-running rebuilds both.
    /// </summary>
    public static class ZoneUnlockBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string DotPath = "Assets/_Project/Art/Sprites/Placeholders/ph_soft_dot.png";
        private const string PrefabPath = "Assets/_Project/Prefabs/VFX/ZoneWave.prefab";
        private const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
        private const string RootName = "ZoneUnlock";
        // Above the ground and plots, under tonics (500) and pop-ups.
        private const int PetalOrder = 400;
        private const int MaxPetals = 60;

        private static readonly Color PetalPink = new Color(1f, 0.72f, 0.82f, 1f);
        private static readonly Color PetalCream = new Color(1f, 0.96f, 0.84f, 1f);

        [MenuItem("Radiant Orchard/Setup/Build Zone Unlock")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("ZoneUnlockBuilder: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            // Load assets after opening the scene: opening a scene unloads unused assets.
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(DotPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            if (dot == null || material == null)
            {
                Debug.LogError($"ZoneUnlockBuilder: missing {DotPath} (run Build Tonic) or {UnlitSpriteMaterialPath}.");
                return;
            }

            GameObject source = BuildPetals(dot, material);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabPath);
            Object.DestroyImmediate(source);

            GameInstaller installer = null;
            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (root.name == RootName) Object.DestroyImmediate(root);
                else if (installer == null) installer = root.GetComponentInChildren<GameInstaller>(true);
            }
            if (installer == null)
            {
                Debug.LogError("ZoneUnlockBuilder: no GameInstaller in Game; run Radiant Orchard > Setup > Add Bootstrapper and GameInstaller first.");
                return;
            }

            var rootGo = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(rootGo, game);
            var petals = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootGo.transform);
            var sequence = rootGo.AddComponent<ZoneUnlockSequence>();
            var so = new SerializedObject(sequence);
            so.FindProperty("_particles").objectReferenceValue = petals.GetComponent<ZoneWaveParticles>();
            so.ApplyModifiedPropertiesWithoutUndo();

            var installerSo = new SerializedObject(installer);
            installerSo.FindProperty("_zoneUnlock").objectReferenceValue = sequence;
            installerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log("ZoneUnlockBuilder: ZoneWave prefab and ZoneUnlock (ZoneUnlockSequence) built and wired into the GameInstaller.");
        }

        // A box emitter across the zone, petals drifting up and fading. Soft dots until the FX-11 petal art exists.
        private static GameObject BuildPetals(Sprite dot, Material material)
        {
            var go = new GameObject("ZoneWave");
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.maxParticles = MaxPetals;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(PetalPink, PetalCream);
            main.gravityModifier = 0.05f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 45f;

            // Box across the zone (width set at runtime), emitting upwards in 2D.
            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            shape.scale = new Vector3(10f, 0.2f, 1f);
            shape.randomDirectionAmount = 0.35f;

            ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;

            ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.SetSprite(0, dot);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sortingOrder = PetalOrder;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            go.AddComponent<ZoneWaveParticles>();
            return go;
        }
    }
}
