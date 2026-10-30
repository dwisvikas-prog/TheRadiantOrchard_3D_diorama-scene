using RadiantOrchard.App;
using RadiantOrchard.Input;
using RadiantOrchard.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RadiantOrchard.Editor
{
    /// <summary>
    /// Builds the placeholder UI in the Game scene: EventSystem (Input System UI module), the overlay Canvas
    /// (1920x1080 reference, match 0.5) and the plant menu, saved as a prefab so it can be restyled without code.
    /// Adds the GestureRecognizer to the camera and wires everything into the GameInstaller. Re-running rebuilds the UI.
    /// </summary>
    public static class UiBuilder
    {
        private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";
        private const string PrefabFolder = "Assets/_Project/Prefabs/UI";
        private const string PlantMenuPrefabPath = PrefabFolder + "/PlantMenu.prefab";
        private const string BoldFontPath = "Assets/_Project/Art/Fonts/Fredoka-Bold SDF.asset";
        private const string CanvasName = "UI";

        private static readonly Color Cream = Hex("#FFF6E3");
        private static readonly Color Accent = Hex("#2F6B4F");
        private static readonly Color Ink = Hex("#3B2A1E");

        [MenuItem("Radiant Orchard/Setup/Build UI (placeholder)")]
        public static void Build()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("UiBuilder: save or discard the open scene's changes first; nothing was changed.");
                    return;
                }
            }

            Scene game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);

            GameInstaller installer = null;
            Camera camera = null;
            foreach (GameObject root in game.GetRootGameObjects())
            {
                if (root.name == CanvasName || root.GetComponent<EventSystem>() != null) Object.DestroyImmediate(root);
                else
                {
                    if (installer == null) installer = root.GetComponentInChildren<GameInstaller>(true);
                    if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
                }
            }
            if (installer == null || camera == null)
            {
                Debug.LogError("UiBuilder: the Game scene needs a GameInstaller and a camera (run the other Setup items first).");
                return;
            }

            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, game);

            var canvasGo = new GameObject(CanvasName, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(canvasGo, game);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureFolder(PrefabFolder);
            GameObject menuSource = BuildPlantMenu(bold);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(menuSource, PlantMenuPrefabPath);
            Object.DestroyImmediate(menuSource);
            var menu = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvasGo.transform);
            Stretch(menu.GetComponent<RectTransform>());
            // The HUD sits under the menu in the hierarchy so the menu's backdrop covers it while open.
            HudView hud = BuildHud(canvasGo.transform, bold);
            hud.transform.SetAsFirstSibling();
            BasketView basket = BuildBasket(hud.transform, bold);
            ComboView combo = BuildCombo(hud.transform, bold);

            // Components whose script was deleted (TapDetector, replaced by GestureRecognizer in W1·19).
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(camera.gameObject);
            var gestures = camera.GetComponent<GestureRecognizer>();
            if (gestures == null) gestures = camera.gameObject.AddComponent<GestureRecognizer>();

            var so = new SerializedObject(installer);
            so.FindProperty("_gestures").objectReferenceValue = gestures;
            so.FindProperty("_plantMenu").objectReferenceValue = menu.GetComponent<PlantMenuView>();
            so.FindProperty("_hud").objectReferenceValue = hud;
            so.FindProperty("_basket").objectReferenceValue = basket;
            so.FindProperty("_combo").objectReferenceValue = combo;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log("UiBuilder: EventSystem, Canvas, HUD (seeds, basket, combo) and PlantMenu prefab built; GestureRecognizer, PlantMenu, HUD, Basket and Combo wired into the GameInstaller.");
        }

        private static GameObject BuildPlantMenu(TMP_FontAsset bold)
        {
            var menu = new GameObject("PlantMenu", typeof(RectTransform), typeof(PlantMenuView));
            var root = Rect("Root", menu.transform);
            Stretch(root);

            var backdrop = Rect("Backdrop", root);
            Stretch(backdrop);
            var backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.35f);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;

            // The panel sits in the safe area; the backdrop above still dims the whole screen.
            var safe = Rect("Safe", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var panel = Rect("Panel", safe);
            Centre(panel, new Vector2(1180f, 720f), Vector2.zero);
            panel.gameObject.AddComponent<Image>().color = Cream;

            TMP_Text title = Text("Title", panel, 56f, Ink, bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(800f, 80f), new Vector2(0f, -60f));
            TMP_Text seeds = Text("Seeds", panel, 34f, Accent, null);
            Anchor(seeds.rectTransform, new Vector2(0.5f, 1f), new Vector2(800f, 50f), new Vector2(0f, -120f));

            var close = Rect("Close", panel);
            Anchor(close, new Vector2(1f, 1f), new Vector2(180f, 70f), new Vector2(-110f, -55f));
            close.gameObject.AddComponent<Image>().color = Accent;
            var closeButton = close.gameObject.AddComponent<Button>();
            TMP_Text closeLabel = Text("Label", close, 30f, Color.white, bold);
            Stretch(closeLabel.rectTransform);

            var options = Rect("Options", panel);
            Anchor(options, new Vector2(0.5f, 0.5f), new Vector2(1120f, 500f), new Vector2(0f, -85f));
            var grid = options.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(360f, 150f);
            grid.spacing = new Vector2(20f, 20f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;

            FruitOptionView template = BuildOption(options, bold);

            var view = menu.GetComponent<PlantMenuView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root.gameObject;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_seeds").objectReferenceValue = seeds;
            so.FindProperty("_closeLabel").objectReferenceValue = closeLabel;
            so.FindProperty("_close").objectReferenceValue = closeButton;
            so.FindProperty("_backdrop").objectReferenceValue = backdropButton;
            so.FindProperty("_optionsParent").objectReferenceValue = options;
            so.FindProperty("_optionTemplate").objectReferenceValue = template;
            so.ApplyModifiedPropertiesWithoutUndo();
            return menu;
        }

        // Seed counter pill in the top-left corner. Text comes from the StringTable at runtime.
        private static HudView BuildHud(Transform canvas, TMP_FontAsset bold)
        {
            var hudRt = Rect("Hud", canvas);
            Stretch(hudRt);
            hudRt.gameObject.AddComponent<SafeArea>();

            var pill = Rect("SeedsPill", hudRt);
            Anchor(pill, new Vector2(0f, 1f), new Vector2(320f, 80f), new Vector2(200f, -70f));
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.color = Cream;
            pillImage.raycastTarget = false;
            TMP_Text seeds = Text("Seeds", pill, 38f, Ink, bold);
            Stretch(seeds.rectTransform);

            var hud = hudRt.gameObject.AddComponent<HudView>();
            var so = new SerializedObject(hud);
            so.FindProperty("_seeds").objectReferenceValue = seeds;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hud;
        }

        // Basket pill under the seed counter: a title (where tonics land) and one chip per fruit, laid out left to right
        // and only as wide as the chips showing.
        private static BasketView BuildBasket(Transform hud, TMP_FontAsset bold)
        {
            var pill = Rect("Basket", hud);
            pill.anchorMin = pill.anchorMax = new Vector2(0f, 1f);
            pill.pivot = new Vector2(0f, 1f);
            pill.sizeDelta = new Vector2(200f, 70f);
            pill.anchoredPosition = new Vector2(40f, -125f);
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.color = Cream;
            pillImage.raycastTarget = false;
            var row = pill.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(18, 18, 8, 8);
            row.spacing = 12f;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            TMP_Text title = Text("Title", pill, 28f, Ink, bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Size(title.rectTransform, 100f, 50f);

            var chip = Rect("ChipTemplate", pill);
            Size(chip, 104f, 54f);
            var icon = Rect("Icon", chip);
            Anchor(icon, new Vector2(0f, 0.5f), new Vector2(40f, 40f), new Vector2(22f, 0f));
            var iconImage = icon.gameObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            TMP_Text count = Text("Count", chip, 28f, Accent, bold);
            count.alignment = TextAlignmentOptions.Left;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(count.rectTransform, new Vector2(0f, 0.5f), new Vector2(60f, 50f), new Vector2(76f, 0f));
            var chipView = chip.gameObject.AddComponent<BasketChipView>();
            var chipSo = new SerializedObject(chipView);
            chipSo.FindProperty("_icon").objectReferenceValue = iconImage;
            chipSo.FindProperty("_count").objectReferenceValue = count;
            chipSo.ApplyModifiedPropertiesWithoutUndo();
            chip.gameObject.SetActive(false);

            var basket = pill.gameObject.AddComponent<BasketView>();
            var so = new SerializedObject(basket);
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_anchor").objectReferenceValue = title.rectTransform;
            so.FindProperty("_chipTemplate").objectReferenceValue = chipView;
            so.ApplyModifiedPropertiesWithoutUndo();
            return basket;
        }

        // Combo counter at the top centre; hidden until the second quick heal.
        private static ComboView BuildCombo(Transform hud, TMP_FontAsset bold)
        {
            var holder = Rect("Combo", hud);
            Stretch(holder);
            var pill = Rect("Pill", holder);
            Anchor(pill, new Vector2(0.5f, 1f), new Vector2(300f, 76f), new Vector2(0f, -70f));
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.color = Accent;
            pillImage.raycastTarget = false;
            TMP_Text text = Text("Text", pill, 38f, Cream, bold);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(text.rectTransform);

            var combo = holder.gameObject.AddComponent<ComboView>();
            var so = new SerializedObject(combo);
            so.FindProperty("_root").objectReferenceValue = pill.gameObject;
            so.FindProperty("_text").objectReferenceValue = text;
            so.ApplyModifiedPropertiesWithoutUndo();
            pill.gameObject.SetActive(false);
            return combo;
        }

        // Fixed preferred size for a child of a layout group.
        private static void Size(RectTransform rt, float width, float height)
        {
            var element = rt.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        private static FruitOptionView BuildOption(Transform parent, TMP_FontAsset bold)
        {
            var option = Rect("OptionTemplate", parent);
            var background = option.gameObject.AddComponent<Image>();
            var button = option.gameObject.AddComponent<Button>();

            var icon = Rect("Icon", option);
            Anchor(icon, new Vector2(0f, 0.5f), new Vector2(110f, 110f), new Vector2(75f, 0f));
            var iconImage = icon.gameObject.AddComponent<Image>();

            TMP_Text name = Text("Name", option, 34f, Ink, bold);
            name.alignment = TextAlignmentOptions.Left;
            Anchor(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(210f, 50f), new Vector2(250f, 25f));
            TMP_Text price = Text("Price", option, 28f, Accent, null);
            price.alignment = TextAlignmentOptions.Left;
            Anchor(price.rectTransform, new Vector2(0f, 0.5f), new Vector2(210f, 40f), new Vector2(250f, -28f));

            var lockRt = Rect("Lock", option);
            Stretch(lockRt);
            var lockImage = lockRt.gameObject.AddComponent<Image>();
            lockImage.color = new Color(0.15f, 0.13f, 0.11f, 0.55f);
            lockImage.raycastTarget = false;
            // Sits on the price line (the price is hidden while locked).
            TMP_Text lockLabel = Text("LockLabel", lockRt, 26f, Color.white, bold);
            lockLabel.alignment = TextAlignmentOptions.Left;
            lockLabel.textWrappingMode = TextWrappingModes.NoWrap;
            lockLabel.enableAutoSizing = true;
            lockLabel.fontSizeMin = 16f;
            lockLabel.fontSizeMax = 26f;
            Anchor(lockLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(210f, 40f), new Vector2(250f, -28f));

            var view = option.gameObject.AddComponent<FruitOptionView>();
            var so = new SerializedObject(view);
            so.FindProperty("_button").objectReferenceValue = button;
            so.FindProperty("_background").objectReferenceValue = background;
            so.FindProperty("_icon").objectReferenceValue = iconImage;
            so.FindProperty("_name").objectReferenceValue = name;
            so.FindProperty("_price").objectReferenceValue = price;
            so.FindProperty("_lock").objectReferenceValue = lockRt.gameObject;
            so.FindProperty("_lockLabel").objectReferenceValue = lockLabel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return view;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // Empty text in the prefab; every label is filled from the StringTable at runtime.
        private static TMP_Text Text(string name, Transform parent, float size, Color colour, TMP_FontAsset font)
        {
            var rt = Rect(name, parent);
            var text = rt.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.text = string.Empty;
            text.fontSize = size;
            text.color = colour;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Centre(RectTransform rt, Vector2 size, Vector2 position) => Anchor(rt, new Vector2(0.5f, 0.5f), size, position);

        private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }
    }
}
