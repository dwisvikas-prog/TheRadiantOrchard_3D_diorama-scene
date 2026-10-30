using System.IO;
using RadiantOrchard.App;
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
    /// Builds the W1·21 stand-in visitor on the first waiting spot (grey body + head, thought bubble with the
    /// requested fruit), the VirtuePopup prefab and the VirtuePopups pool, and wires them into the GameInstaller.
    /// Placeholder shapes until the stick-figure art (CHAR) exists. Re-running rebuilds them.
    /// </summary>
    public static class VisitorBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string PlaceholderFolder = "Assets/_Project/Art/Sprites/Placeholders";
        private const string PrefabFolder = "Assets/_Project/Prefabs/VFX";
        private const string PopupPrefabPath = PrefabFolder + "/VirtuePopup.prefab";
        private const string FontPath = "Assets/_Project/Art/Fonts/Fredoka-Bold SDF.asset";
        private const string VisitorName = "PlaceholderVisitor";
        private const string PopupsName = "VirtuePopups";
        // Above tonics (500) so the virtue name is never hidden by a landing tonic.
        private const int PopupOrder = 600;

        [MenuItem("Radiant Orchard/Setup/Build Placeholder Visitor")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("VisitorBuilder: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            // Load assets after opening the scene: opening a scene unloads unused assets.
            var block = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_block.png");
            var dot = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_soft_dot.png");
            var diamond = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderFolder + "/ph_tile.png");
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if (block == null || dot == null || diamond == null || font == null)
            {
                Debug.LogError("VisitorBuilder: placeholder sprites or the Fredoka-Bold SDF font are missing; run Build Placeholder Farm and Build Tonic first.");
                return;
            }

            GameInstaller installer = null;
            FarmView farm = null;
            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (root.name == VisitorName || root.name == PopupsName)
                {
                    Object.DestroyImmediate(root);
                    continue;
                }
                if (installer == null) installer = root.GetComponentInChildren<GameInstaller>(true);
                if (farm == null) farm = root.GetComponentInChildren<FarmView>(true);
            }
            if (installer == null || farm == null || farm.WaitingSpots.Count == 0)
            {
                Debug.LogError("VisitorBuilder: the Game scene needs a GameInstaller and a farm with waiting spots (run the other Setup items first).");
                return;
            }

            EnsureFolder(PrefabFolder);
            GameObject popupSource = BuildPopup(font);
            var popupPrefab = PrefabUtility.SaveAsPrefabAsset(popupSource, PopupPrefabPath).GetComponent<VirtuePopup>();
            Object.DestroyImmediate(popupSource);

            var visitor = BuildVisitor(block, dot, diamond, farm.WaitingSpots[0].position);
            SceneManager.MoveGameObjectToScene(visitor.gameObject, game);

            var popupsGo = new GameObject(PopupsName);
            SceneManager.MoveGameObjectToScene(popupsGo, game);
            var popups = popupsGo.AddComponent<VirtuePopups>();
            var popupsSo = new SerializedObject(popups);
            popupsSo.FindProperty("_prefab").objectReferenceValue = popupPrefab;
            popupsSo.ApplyModifiedPropertiesWithoutUndo();

            var installerSo = new SerializedObject(installer);
            installerSo.FindProperty("_visitor").objectReferenceValue = visitor;
            installerSo.FindProperty("_popups").objectReferenceValue = popups;
            installerSo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log("VisitorBuilder: placeholder visitor, VirtuePopup prefab and VirtuePopups built and wired into the GameInstaller.");
        }

        private static PlaceholderVisitor BuildVisitor(Sprite block, Sprite dot, Sprite diamond, Vector3 position)
        {
            var root = new GameObject(VisitorName);
            root.transform.position = position;
            // Body, head and bubble sort as one object, by the visitor's feet.
            root.AddComponent<SortingGroup>();

            SpriteRenderer body = Renderer(root.transform, "Body", block, 0, Vector3.zero, SizeOf(block, 0.55f, 1.3f));
            SpriteRenderer head = Renderer(root.transform, "Head", dot, 1, new Vector3(0f, 1.6f, 0f), SizeOf(dot, 0.7f, 0.7f));

            var bubble = new GameObject("Bubble");
            bubble.transform.SetParent(root.transform, false);
            bubble.transform.localPosition = new Vector3(0.95f, 2.55f, 0f);
            Renderer(bubble.transform, "Back", dot, 2, Vector3.zero, SizeOf(dot, 1.2f, 1.0f)).color = Color.white;
            Renderer(bubble.transform, "Tail", dot, 2, new Vector3(-0.5f, -0.6f, 0f), SizeOf(dot, 0.3f, 0.3f)).color = Color.white;
            SpriteRenderer fruit = Renderer(bubble.transform, "Fruit", diamond, 3, Vector3.zero, SizeOf(diamond, 0.55f, 0.3f));

            var anchor = new GameObject("PopupAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(0f, 2.3f, 0f);

            var visitor = root.AddComponent<PlaceholderVisitor>();
            var so = new SerializedObject(visitor);
            SerializedProperty parts = so.FindProperty("_body");
            parts.arraySize = 2;
            parts.GetArrayElementAtIndex(0).objectReferenceValue = body;
            parts.GetArrayElementAtIndex(1).objectReferenceValue = head;
            so.FindProperty("_bubble").objectReferenceValue = bubble;
            so.FindProperty("_bubbleFruit").objectReferenceValue = fruit;
            so.FindProperty("_popupAnchor").objectReferenceValue = anchor;
            so.ApplyModifiedPropertiesWithoutUndo();
            return visitor;
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

            var popup = go.AddComponent<VirtuePopup>();
            var so = new SerializedObject(popup);
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static SpriteRenderer Renderer(Transform parent, string name, Sprite sprite, int order, Vector3 localPosition, Vector3 scale)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
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
