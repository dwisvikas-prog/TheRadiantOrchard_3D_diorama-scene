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
        private const string QuizPanelPrefabPath = PrefabFolder + "/QuizPanel.prefab";
        private const string EdgeGlowPath = "Assets/_Project/Art/Sprites/Placeholders/ph_edge_glow.png";
        // Width of the glow's soft border in the sprite, in pixels.
        private const int EdgeGlowBorder = 48;
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
            EnsureEdgeGlowSprite();
            // Opening a scene unloads unused assets, so load these after it.
            var edgeGlow = AssetDatabase.LoadAssetAtPath<Sprite>(EdgeGlowPath);
            bold = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(BoldFontPath);
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
            VibrancyMeterView meter = BuildMeter(hud.transform, bold);
            ZoneBannerView banner = BuildBanner(hud.transform, bold);
            // Under the HUD (the pills stay readable) and the panels: the gold edge glow and its timer.
            GoldenHarvestView goldenHarvest = BuildGoldenHarvest(canvasGo.transform, bold, edgeGlow);
            goldenHarvest.transform.SetAsFirstSibling();

            GameObject quizSource = BuildQuizPanel(bold);
            GameObject quizPrefab = PrefabUtility.SaveAsPrefabAsset(quizSource, QuizPanelPrefabPath);
            Object.DestroyImmediate(quizSource);
            var quiz = (GameObject)PrefabUtility.InstantiatePrefab(quizPrefab, canvasGo.transform);
            Stretch(quiz.GetComponent<RectTransform>());
            // Last, so the bloom's verse card covers everything.
            VersePanelView verse = BuildVersePanel(canvasGo.transform, bold);

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
            so.FindProperty("_meter").objectReferenceValue = meter;
            so.FindProperty("_banner").objectReferenceValue = banner;
            so.FindProperty("_quizPanel").objectReferenceValue = quiz.GetComponent<QuizPanelView>();
            so.FindProperty("_goldenHarvest").objectReferenceValue = goldenHarvest;
            so.FindProperty("_versePanel").objectReferenceValue = verse;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            Debug.Log("UiBuilder: EventSystem, Canvas, HUD (seeds, basket, combo, vibrancy meter, zone banner, Golden Harvest), verse card, PlantMenu and QuizPanel prefabs built; GestureRecognizer, PlantMenu, HUD, Basket, Combo, Meter, Banner, QuizPanel, Golden Harvest and Verse Panel wired into the GameInstaller.");
        }

        // The bloom's verse card (CLAUDE.md 5.9, UI-10 placeholder): dimmed screen, a cream card with the title, the verse,
        // its reference and "Tap to continue"; a tap anywhere closes it. Its own Canvas, so the fade does not rebuild the HUD.
        private static VersePanelView BuildVersePanel(Transform canvas, TMP_FontAsset bold)
        {
            var holder = Rect("VersePanel", canvas);
            Stretch(holder);
            holder.gameObject.AddComponent<Canvas>();
            holder.gameObject.AddComponent<GraphicRaycaster>();

            var root = Rect("Root", holder);
            Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            root.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
            var backdrop = root.gameObject.AddComponent<Button>();
            backdrop.transition = Selectable.Transition.None;

            var safe = Rect("Safe", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var card = Rect("Card", safe);
            Centre(card, new Vector2(1300f, 620f), Vector2.zero);
            var cardImage = card.gameObject.AddComponent<Image>();
            cardImage.color = Cream;
            cardImage.raycastTarget = false;
            var stripe = Rect("Stripe", card);
            Anchor(stripe, new Vector2(0.5f, 1f), new Vector2(1300f, 14f), new Vector2(0f, -7f));
            var stripeImage = stripe.gameObject.AddComponent<Image>();
            stripeImage.color = Accent;
            stripeImage.raycastTarget = false;

            TMP_Text title = Text("Title", card, 60f, Accent, bold);
            title.enableAutoSizing = true;
            title.fontSizeMin = 36f;
            title.fontSizeMax = 60f;
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(1200f, 90f), new Vector2(0f, -80f));
            TMP_Text verse = Text("Verse", card, 42f, Ink, bold);
            verse.enableAutoSizing = true;
            verse.fontSizeMin = 26f;
            verse.fontSizeMax = 42f;
            Anchor(verse.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(1160f, 280f), new Vector2(0f, 10f));
            TMP_Text reference = Text("Reference", card, 36f, Accent, bold);
            Anchor(reference.rectTransform, new Vector2(0.5f, 0f), new Vector2(1160f, 60f), new Vector2(0f, 120f));
            TMP_Text more = Text("Continue", card, 28f, Ink, bold);
            more.alpha = 0.6f;
            Anchor(more.rectTransform, new Vector2(0.5f, 0f), new Vector2(1160f, 50f), new Vector2(0f, 50f));

            var view = holder.gameObject.AddComponent<VersePanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root.gameObject;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_backdrop").objectReferenceValue = backdrop;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_verse").objectReferenceValue = verse;
            so.FindProperty("_reference").objectReferenceValue = reference;
            so.FindProperty("_continue").objectReferenceValue = more;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            return view;
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

        // Combo counter at the top centre under the meter; hidden until the second quick heal.
        private static ComboView BuildCombo(Transform hud, TMP_FontAsset bold)
        {
            var holder = Rect("Combo", hud);
            Stretch(holder);
            var pill = Rect("Pill", holder);
            Anchor(pill, new Vector2(0.5f, 1f), new Vector2(300f, 76f), new Vector2(0f, -165f));
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

        // Vibrancy Meter at the top centre: stage label, a bar that fills towards the next stage, and the heal count.
        private static VibrancyMeterView BuildMeter(Transform hud, TMP_FontAsset bold)
        {
            var pill = Rect("VibrancyMeter", hud);
            Anchor(pill, new Vector2(0.5f, 1f), new Vector2(640f, 80f), new Vector2(0f, -70f));
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.color = Cream;
            pillImage.raycastTarget = false;

            TMP_Text stage = Text("Stage", pill, 34f, Ink, bold);
            stage.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(stage.rectTransform, new Vector2(0f, 0.5f), new Vector2(150f, 60f), new Vector2(100f, 0f));

            var bar = Rect("Bar", pill);
            Anchor(bar, new Vector2(0.5f, 0.5f), new Vector2(280f, 28f), new Vector2(-5f, 0f));
            var barImage = bar.gameObject.AddComponent<Image>();
            barImage.color = new Color(Ink.r, Ink.g, Ink.b, 0.18f);
            barImage.raycastTarget = false;
            var fill = Rect("Fill", bar);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;
            var fillImage = fill.gameObject.AddComponent<Image>();
            fillImage.color = Accent;
            fillImage.raycastTarget = false;

            TMP_Text count = Text("Count", pill, 30f, Accent, bold);
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.enableAutoSizing = true;
            count.fontSizeMin = 18f;
            count.fontSizeMax = 30f;
            Anchor(count.rectTransform, new Vector2(1f, 0.5f), new Vector2(160f, 60f), new Vector2(-95f, 0f));

            var meter = pill.gameObject.AddComponent<VibrancyMeterView>();
            var so = new SerializedObject(meter);
            so.FindProperty("_stage").objectReferenceValue = stage;
            so.FindProperty("_count").objectReferenceValue = count;
            so.FindProperty("_fill").objectReferenceValue = fill;
            so.ApplyModifiedPropertiesWithoutUndo();
            return meter;
        }

        // Zone unlock banner in the upper middle of the screen; hidden until a stage is reached. The view sits on an
        // always-active holder so it can fade the banner out.
        private static ZoneBannerView BuildBanner(Transform hud, TMP_FontAsset bold)
        {
            var holder = Rect("ZoneBanner", hud);
            Stretch(holder);
            var panel = Rect("Panel", holder);
            Anchor(panel, new Vector2(0.5f, 0.5f), new Vector2(900f, 260f), new Vector2(0f, 230f));
            var panelImage = panel.gameObject.AddComponent<Image>();
            panelImage.color = Cream;
            panelImage.raycastTarget = false;
            var group = panel.gameObject.AddComponent<CanvasGroup>();
            group.interactable = false;
            group.blocksRaycasts = false;

            var stripe = Rect("Stripe", panel);
            Anchor(stripe, new Vector2(0.5f, 1f), new Vector2(900f, 14f), new Vector2(0f, -7f));
            var stripeImage = stripe.gameObject.AddComponent<Image>();
            stripeImage.color = Accent;
            stripeImage.raycastTarget = false;

            TMP_Text title = Text("Title", panel, 64f, Ink, bold);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(840f, 90f), new Vector2(0f, -75f));
            TMP_Text body = Text("Body", panel, 36f, Accent, bold);
            body.enableAutoSizing = true;
            body.fontSizeMin = 22f;
            body.fontSizeMax = 36f;
            Anchor(body.rectTransform, new Vector2(0.5f, 0f), new Vector2(840f, 120f), new Vector2(0f, 80f));

            var banner = holder.gameObject.AddComponent<ZoneBannerView>();
            var so = new SerializedObject(banner);
            so.FindProperty("_root").objectReferenceValue = panel.gameObject;
            so.FindProperty("_group").objectReferenceValue = group;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_body").objectReferenceValue = body;
            so.ApplyModifiedPropertiesWithoutUndo();
            panel.gameObject.SetActive(false);
            return banner;
        }

        // The Wisdom Tree's bottom sheet (CLAUDE.md 5.8): scenario, three fruit choices, hint line, close button.
        private static GameObject BuildQuizPanel(TMP_FontAsset bold)
        {
            var quiz = new GameObject("QuizPanel", typeof(RectTransform), typeof(QuizPanelView));
            var root = Rect("Root", quiz.transform);
            Stretch(root);

            var backdrop = Rect("Backdrop", root);
            Stretch(backdrop);
            backdrop.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.35f);
            var backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;

            var safe = Rect("Safe", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var sheet = Rect("Sheet", safe);
            sheet.anchorMin = sheet.anchorMax = new Vector2(0.5f, 0f);
            sheet.pivot = new Vector2(0.5f, 0f);
            sheet.sizeDelta = new Vector2(1400f, 560f);
            sheet.anchoredPosition = new Vector2(0f, 0f);
            sheet.gameObject.AddComponent<Image>().color = Cream;

            TMP_Text title = Text("Title", sheet, 44f, Accent, bold);
            Anchor(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 70f), new Vector2(0f, -50f));
            TMP_Text scenario = Text("Scenario", sheet, 38f, Ink, bold);
            scenario.enableAutoSizing = true;
            scenario.fontSizeMin = 26f;
            scenario.fontSizeMax = 38f;
            Anchor(scenario.rectTransform, new Vector2(0.5f, 1f), new Vector2(1280f, 130f), new Vector2(0f, -155f));

            var close = Rect("Close", sheet);
            Anchor(close, new Vector2(1f, 1f), new Vector2(180f, 70f), new Vector2(-110f, -50f));
            close.gameObject.AddComponent<Image>().color = Accent;
            var closeButton = close.gameObject.AddComponent<Button>();
            TMP_Text closeLabel = Text("Label", close, 30f, Color.white, bold);
            Stretch(closeLabel.rectTransform);

            var buttons = new Button[3];
            var icons = new Image[3];
            var names = new TMP_Text[3];
            for (int i = 0; i < 3; i++)
            {
                var option = Rect("Option_" + (i + 1), sheet);
                Anchor(option, new Vector2(0.5f, 0f), new Vector2(420f, 160f), new Vector2((i - 1) * 440f, 175f));
                option.gameObject.AddComponent<Image>().color = Color.white;
                buttons[i] = option.gameObject.AddComponent<Button>();
                var icon = Rect("Icon", option);
                Anchor(icon, new Vector2(0f, 0.5f), new Vector2(110f, 110f), new Vector2(80f, 0f));
                icons[i] = icon.gameObject.AddComponent<Image>();
                icons[i].raycastTarget = false;
                names[i] = Text("Name", option, 38f, Ink, bold);
                names[i].alignment = TextAlignmentOptions.Left;
                names[i].enableAutoSizing = true;
                names[i].fontSizeMin = 24f;
                names[i].fontSizeMax = 38f;
                Anchor(names[i].rectTransform, new Vector2(0f, 0.5f), new Vector2(250f, 80f), new Vector2(285f, 0f));
            }

            TMP_Text hint = Text("Hint", sheet, 32f, Accent, bold);
            hint.enableAutoSizing = true;
            hint.fontSizeMin = 22f;
            hint.fontSizeMax = 32f;
            Anchor(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(1280f, 70f), new Vector2(0f, 50f));
            hint.gameObject.SetActive(false);

            var view = quiz.GetComponent<QuizPanelView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root.gameObject;
            so.FindProperty("_title").objectReferenceValue = title;
            so.FindProperty("_scenario").objectReferenceValue = scenario;
            so.FindProperty("_hint").objectReferenceValue = hint;
            so.FindProperty("_close").objectReferenceValue = closeButton;
            so.FindProperty("_closeLabel").objectReferenceValue = closeLabel;
            so.FindProperty("_backdrop").objectReferenceValue = backdropButton;
            SetArray(so.FindProperty("_optionButtons"), buttons);
            SetArray(so.FindProperty("_optionIcons"), icons);
            SetArray(so.FindProperty("_optionNames"), names);
            so.ApplyModifiedPropertiesWithoutUndo();
            return quiz;
        }

        // Golden Harvest (5.8, FX-12 placeholder): a gold glow around the whole screen (a sliced sprite with no centre)
        // and a timer pill in the safe area. Its own Canvas, so the pulse does not rebuild the HUD.
        private static GoldenHarvestView BuildGoldenHarvest(Transform canvas, TMP_FontAsset bold, Sprite edgeGlow)
        {
            var holder = Rect("GoldenHarvest", canvas);
            Stretch(holder);
            holder.gameObject.AddComponent<Canvas>();
            var root = Rect("Root", holder);
            Stretch(root);

            var glowRt = Rect("EdgeGlow", root);
            Stretch(glowRt);
            var glow = glowRt.gameObject.AddComponent<Image>();
            glow.sprite = edgeGlow;
            glow.type = Image.Type.Sliced;
            glow.fillCenter = false;
            // The border shows about 110 canvas units wide.
            glow.pixelsPerUnitMultiplier = EdgeGlowBorder / 110f;
            glow.color = new Color(1f, 0.82f, 0.35f, 1f);
            glow.raycastTarget = false;
            var glowGroup = glowRt.gameObject.AddComponent<CanvasGroup>();
            glowGroup.interactable = false;
            glowGroup.blocksRaycasts = false;

            var safe = Rect("Safe", root);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            var pill = Rect("TimerPill", safe);
            Anchor(pill, new Vector2(0.5f, 1f), new Vector2(420f, 72f), new Vector2(0f, -255f));
            var pillImage = pill.gameObject.AddComponent<Image>();
            pillImage.color = new Color(0.86f, 0.62f, 0.12f, 1f);
            pillImage.raycastTarget = false;
            TMP_Text timer = Text("Timer", pill, 34f, Color.white, bold);
            timer.textWrappingMode = TextWrappingModes.NoWrap;
            Stretch(timer.rectTransform);

            var view = holder.gameObject.AddComponent<GoldenHarvestView>();
            var so = new SerializedObject(view);
            so.FindProperty("_root").objectReferenceValue = root.gameObject;
            so.FindProperty("_glow").objectReferenceValue = glowGroup;
            so.FindProperty("_timer").objectReferenceValue = timer;
            so.ApplyModifiedPropertiesWithoutUndo();
            root.gameObject.SetActive(false);
            return view;
        }

        // A square that is warm gold at its edges and fades to clear within EdgeGlowBorder pixels; sliced, centre unused.
        private static void EnsureEdgeGlowSprite()
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(EdgeGlowPath) != null) return;

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                    float a = Mathf.Clamp01(1f - d / EdgeGlowBorder);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }
            System.IO.File.WriteAllBytes(EdgeGlowPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(EdgeGlowPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(EdgeGlowPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = new Vector4(EdgeGlowBorder, EdgeGlowBorder, EdgeGlowBorder, EdgeGlowBorder);
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void SetArray<T>(SerializedProperty array, T[] items) where T : Object
        {
            array.arraySize = items.Length;
            for (int i = 0; i < items.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
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
