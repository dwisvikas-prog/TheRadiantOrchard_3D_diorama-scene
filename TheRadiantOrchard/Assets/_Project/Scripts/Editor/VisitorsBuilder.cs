using System.IO;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Presentation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the visitor pieces: the default VisitorDefinition (added to GameConfig), the pooled Visitor prefab
    /// (placeholder body, head, thought bubble, patience bar, speech line), the VirtuePopup prefab, and the "Visitors" and
    /// "VirtuePopups" objects in the Game scene, wired into the GameInstaller. Replaces the W1·21 stand-in visitor.
    /// Placeholder shapes until the stick-figure art (CHAR) exists. Re-running rebuilds everything.
    /// </summary>
    public static class VisitorsBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string GameConfigPath = "Assets/_Project/Data/GameConfig.asset";
        private const string VisitorDataFolder = "Assets/_Project/Data/Visitors";
        private const string VisitorDataPath = VisitorDataFolder + "/Visitor_Default.asset";
        private const string PlaceholderFolder = "Assets/_Project/Art/Sprites/Placeholders";
        private const string VisitorPrefabFolder = "Assets/_Project/Prefabs/Visitors";
        private const string VisitorPrefabPath = VisitorPrefabFolder + "/Visitor.prefab";
        private const string PopupPrefabPath = "Assets/_Project/Prefabs/VFX/VirtuePopup.prefab";
        private const string FontPath = "Assets/_Project/Art/Fonts/Fredoka-Bold SDF.asset";
        private const string VisitorsName = "Visitors";
        private const string PopupsName = "VirtuePopups";
        private const string OldStandInName = "PlaceholderVisitor";
        // Above tonics (500) so the virtue name is never hidden by a landing tonic.
        private const int PopupOrder = 600;
        private static readonly Color BarBack = new Color(0.2f, 0.18f, 0.16f, 0.85f);
        private static readonly Color LineBackColour = new Color(1f, 0.97f, 0.88f, 0.92f);
        private static readonly Color LineTextColour = new Color(0.25f, 0.2f, 0.15f, 1f);

        [MenuItem("Radiant Orchard/Setup/Build Visitors")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("VisitorsBuilder: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            EnsureFolder(VisitorDataFolder);
            EnsureFolder(VisitorPrefabFolder);
            EnsureFolder(Path.GetDirectoryName(PopupPrefabPath).Replace('\\', '/'));
            EnsureVisitorDefinition();

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            // Load assets after opening the scene: opening a scene unloads unused assets.
            var block = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_block.png");
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_soft_dot.png");
            var diamond = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_tile.png");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Material desaturate = DesaturateMaterial.Ensure();
            if (block == null || dot == null || diamond == null || font == null || desaturate == null)
            {
                Debug.LogError("VisitorsBuilder: placeholder sprites or the Fredoka-Bold SDF font are missing; run Build Placeholder Farm and Build Tonic first.");
                return;
            }

            GameInstaller installer = null;
            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (root.name == VisitorsName || root.name == PopupsName || root.name == OldStandInName)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                if (installer == null) installer = root.GetComponentInChildren<GameInstaller>(true);
            }
            if (installer == null)
            {
                Debug.LogError("VisitorsBuilder: no GameInstaller in Game; run Radiant Orchard > Setup > Add Bootstrapper and GameInstaller first.");
                return;
            }

            GameObject visitorSource = BuildVisitor(block, dot, diamond, font, desaturate);
            var visitorPrefab = PrefabUtility.SaveAsPrefabAsset(visitorSource, VisitorPrefabPath).GetComponent<VisitorView>();
            Object.DestroyImmediate(visitorSource);
            GameObject popupSource = BuildPopup(font);
            var popupPrefab = PrefabUtility.SaveAsPrefabAsset(popupSource, PopupPrefabPath).GetComponent<VirtuePopup>();
            Object.DestroyImmediate(popupSource);

            var visitorsGo = new GameObject(VisitorsName);
            SceneManager.MoveGameObjectToScene(visitorsGo, game);
            var visitors = visitorsGo.AddComponent<VisitorsPresenter>();
            SetReference(visitors, "_prefab", visitorPrefab);

            var popupsGo = new GameObject(PopupsName);
            SceneManager.MoveGameObjectToScene(popupsGo, game);
            var popups = popupsGo.AddComponent<VirtuePopups>();
            SetReference(popups, "_prefab", popupPrefab);

            var installerSo = new SerializedObject(installer);
            installerSo.FindProperty("_visitorViews").objectReferenceValue = visitors;
            installerSo.FindProperty("_popups").objectReferenceValue = popups;
            installerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log("VisitorsBuilder: Visitor definition, Visitor and VirtuePopup prefabs, Visitors and VirtuePopups built and wired into the GameInstaller.");
        }

        private static void EnsureVisitorDefinition()
        {
            var definition = AssetDatabase.LoadAssetAtPath<VisitorDefinition>(VisitorDataPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<VisitorDefinition>();
                AssetDatabase.CreateAsset(definition, VisitorDataPath);
            }

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            var so = new SerializedObject(config);
            SerializedProperty list = so.FindProperty("_visitors");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == definition) return;
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = definition;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildVisitor(Sprite block, Sprite dot, Sprite diamond, TMP_FontAsset font, Material desaturate)
        {
            var root = new GameObject("Visitor");
            // Body, head, bubble and bar sort as one object, by the visitor's feet.
            root.AddComponent<SortingGroup>();

            SpriteRenderer body = Renderer(root.transform, "Body", block, 0, Vector3.zero, SizeOf(block, 0.55f, 1.3f));
            SpriteRenderer head = Renderer(root.transform, "Head", dot, 1, new Vector3(0f, 1.6f, 0f), SizeOf(dot, 0.7f, 0.7f));
            // Body and head are painted in colour and shown grey by the shader until healed (CLAUDE.md 5.6).
            body.sharedMaterial = desaturate;
            head.sharedMaterial = desaturate;
            var colour = root.AddComponent<Desaturation>();
            var colourSo = new SerializedObject(colour);
            SerializedProperty renderers = colourSo.FindProperty("_renderers");
            renderers.arraySize = 2;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = body;
            renderers.GetArrayElementAtIndex(1).objectReferenceValue = head;
            colourSo.FindProperty("_saturation").floatValue = 0f;
            colourSo.ApplyModifiedPropertiesWithoutUndo();

            var bubble = Child(root.transform, "Bubble", new Vector3(0.95f, 2.55f, 0f));
            Renderer(bubble, "Back", dot, 2, Vector3.zero, SizeOf(dot, 1.2f, 1.0f)).color = Color.white;
            Renderer(bubble, "Tail", dot, 2, new Vector3(-0.5f, -0.6f, 0f), SizeOf(dot, 0.3f, 0.3f)).color = Color.white;
            SpriteRenderer fruit = Renderer(bubble, "Fruit", diamond, 3, Vector3.zero, SizeOf(diamond, 0.55f, 0.3f));

            // Patience bar under the bubble: a dark back and a fill that shrinks from the right.
            var bar = Child(root.transform, "PatienceBar", new Vector3(0.95f, 1.9f, 0f));
            Renderer(bar, "Back", block, 4, Vector3.zero, SizeOf(block, 0.9f, 0.14f)).color = BarBack;
            SpriteRenderer fill = Renderer(bar, "Fill", block, 5, new Vector3(0f, 0.03f, 0f), SizeOf(block, 0.84f, 0.08f));

            Transform popupAnchor = Child(root.transform, "PopupAnchor", new Vector3(0f, 2.3f, 0f));
            Transform tonicAnchor = Child(root.transform, "TonicAnchor", new Vector3(0f, 1.2f, 0f));

            // Speech line above the bubble; the view sizes the backing to the text on each arrival.
            SpriteRenderer lineBack = Renderer(root.transform, "LineBack", block, 6, new Vector3(0f, 3.8f, 0f), Vector3.one);
            lineBack.color = LineBackColour;
            lineBack.gameObject.SetActive(false);
            var lineGo = new GameObject("Line");
            lineGo.transform.SetParent(root.transform, false);
            lineGo.transform.localPosition = new Vector3(0f, 3.6f, 0f);
            var line = lineGo.AddComponent<TextMeshPro>();
            line.font = font;
            line.text = string.Empty;
            line.fontSize = 3.8f;
            line.color = LineTextColour;
            line.alignment = TextAlignmentOptions.Bottom;
            line.textWrappingMode = TextWrappingModes.Normal;
            line.rectTransform.sizeDelta = new Vector2(5f, 2f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.sortingOrder = 7;
            lineGo.SetActive(false);

            var view = root.AddComponent<VisitorView>();
            var so = new SerializedObject(view);
            SerializedProperty parts = so.FindProperty("_body");
            parts.arraySize = 2;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = body;
            parts.GetArrayElementAtIndex(1).objectReferenceValue = head;
            so.FindProperty("_colour").objectReferenceValue = colour;
            so.FindProperty("_bubble").objectReferenceValue = bubble.gameObject;
            so.FindProperty("_bubbleFruit").objectReferenceValue = fruit;
            so.FindProperty("_patienceFill").objectReferenceValue = fill.transform;
            so.FindProperty("_patienceFillRenderer").objectReferenceValue = fill;
            so.FindProperty("_popupAnchor").objectReferenceValue = popupAnchor;
            so.FindProperty("_tonicAnchor").objectReferenceValue = tonicAnchor;
            so.FindProperty("_line").objectReferenceValue = line;
            so.FindProperty("_lineBack").objectReferenceValue = lineBack;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameObject BuildPopup(TMP_FontAsset font)
        {
            var go = new GameObject("VirtuePopup");
            var text = go.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = string.Empty;
            text.fontSize = 7f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.rectTransform.sizeDelta = new Vector2(4f, 1f);
            text.sortingOrder = PopupOrder;
            SetReference(go.AddComponent<VirtuePopup>(), "_text", text);
            return go;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(property).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform Child(Transform parent, string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go.transform;
        }

        private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order, Vector3 localPosition, Vector3 scale)
        {
            Transform t = Child(parent, name, localPosition);
            t.localScale = scale;
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        // Scale that makes the sprite this many world units wide and tall.
        private static Vector3 SizeOf(Sprite sprite, float width, float height) =>
            new Vector3(width / (sprite.rect.width / sprite.pixelsPerUnit), height / (sprite.rect.height / sprite.pixelsPerUnit), 1f);

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
