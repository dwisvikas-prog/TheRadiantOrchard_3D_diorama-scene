using System;
using RadiantOrchard.Gameplay;
using RadiantOrchard.UI;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RadiantOrchard.Dev
{
    /// <summary>
    /// The in-game dev panel (ENGINEERING_GUIDE 9.2): a DEV button and FPS readout in the top-right of the safe area,
    /// and a panel of time skips and cheats acting on DevGame.Current. It exists only in the Editor and development
    /// builds (this assembly) and is built in code at startup, so no prefab or scene object ships in release.
    /// uGUI, so its touches never reach the farm and an idle panel allocates nothing per frame.
    /// </summary>
    public sealed class DevPanel : MonoBehaviour
    {
        private const float FpsRefreshSeconds = 0.5f;
        private const int MaxFpsShown = 240;
        private const int SeedsToAdd = 2000;

        private static readonly Color PanelColour = new Color(0.08f, 0.08f, 0.1f, 0.88f);
        private static readonly Color ButtonColour = new Color(0.22f, 0.36f, 0.3f, 1f);
        private static readonly Color DangerColour = new Color(0.55f, 0.2f, 0.18f, 1f);

        private readonly string[] _fpsTexts = new string[MaxFpsShown + 1];
        private GameObject _panel;
        private TMP_Text _fps;
        private TMP_Text _status;
        private TMP_Text _stage;
        private int _frames;
        private float _elapsed;

        public bool IsOpen => _panel != null && _panel.activeSelf;
        public int ButtonCount { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            var go = new GameObject(nameof(DevPanel));
            DontDestroyOnLoad(go);
            go.AddComponent<DevPanel>();
        }

        public void Toggle()
        {
            _panel.SetActive(!_panel.activeSelf);
            if (_panel.activeSelf) RefreshStage();
        }

        private void Awake()
        {
            for (int i = 0; i <= MaxFpsShown; i++) _fpsTexts[i] = "FPS " + i;
            Build();
        }

        // Counts frames and only swaps in a pre-made string twice a second, so the readout allocates nothing.
        private void Update()
        {
            _frames++;
            _elapsed += Time.unscaledDeltaTime;
            if (_elapsed < FpsRefreshSeconds) return;
            _fps.text = _fpsTexts[Mathf.Clamp(Mathf.RoundToInt(_frames / _elapsed), 0, MaxFpsShown)];
            _frames = 0;
            _elapsed = 0f;
        }

        private void Run(Func<DevGame, string> action)
        {
            _status.text = action(DevGame.Current);
            RefreshStage();
        }

        private void RefreshStage()
        {
            DevGame game = DevGame.Current;
            _stage.text = game != null ? "Stage " + game.State.VibrancyStage : "Stage -";
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform safe = Rect("Safe", transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            Button toggle = MakeButton(safe, "DEV", ButtonColour, Toggle);
            Place((RectTransform)toggle.transform, new Vector2(1f, 1f), new Vector2(140f, 64f), new Vector2(-90f, -52f));
            _fps = Label(safe, "FPS -", 30f, TextAlignmentOptions.Right);
            Place(_fps.rectTransform, new Vector2(1f, 1f), new Vector2(180f, 64f), new Vector2(-260f, -52f));

            RectTransform panel = Rect("Panel", safe);
            Place(panel, new Vector2(1f, 1f), new Vector2(640f, 900f), new Vector2(-340f, -550f));
            panel.gameObject.AddComponent<Image>().color = PanelColour;
            var column = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(20, 20, 20, 20);
            column.spacing = 12f;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandHeight = false;
            _panel = panel.gameObject;

            Header(panel, "Time");
            Row(panel, ("+1 min", g => DevActions.AddTime(g, TimeSpan.FromMinutes(1))),
                       ("+1 hr", g => DevActions.AddTime(g, TimeSpan.FromHours(1))),
                       ("+1 day", g => DevActions.AddTime(g, TimeSpan.FromDays(1))));
            Row(panel, ("Real time", DevActions.ResetTime), ("Golden Harvest", DevActions.GoldenHarvest), ("Quiz now", DevActions.QuizNow));
            Header(panel, "Economy");
            Row(panel, ($"+{SeedsToAdd} seeds", g => DevActions.AddSeeds(g, SeedsToAdd)), ("Ripen all", DevActions.RipenAll), ("Ripen golden", DevActions.RipenGolden));
            _stage = Header(panel, "Stage -");
            Row(panel, ("Stage -1", g => DevActions.SetStage(g, g == null ? 0 : g.State.VibrancyStage - 1)),
                       ("Stage +1", g => DevActions.SetStage(g, g == null ? 0 : g.State.VibrancyStage + 1)));
            Header(panel, "Visitors");
            Row(panel, ("Visitor now", DevActions.SpawnVisitor), ("Heal 1", g => DevActions.Heal(g, 1)), ("Heal 10", g => DevActions.Heal(g, 10)));
            Row(panel, ("Run out patience", DevActions.RunOutPatience), ("Away 10 min", g => DevActions.ArriveAfterAway(g, TimeSpan.FromMinutes(10))));
            Header(panel, "Save");
            RectTransform saveRow = Row(panel, ("Dump save", DevActions.DumpSave));
            Button delete = MakeButton(saveRow, "Delete save", DangerColour, () => Run(DevActions.DeleteSave));
            Fill(delete);
            _status = Label(panel, "Ready.", 24f, TextAlignmentOptions.TopLeft);
            _status.textWrappingMode = TextWrappingModes.Normal;
            Fill(_status, 90f);

            _panel.SetActive(false);
        }

        private TMP_Text Header(Transform parent, string text)
        {
            TMP_Text label = Label(parent, text, 28f, TextAlignmentOptions.Left);
            label.color = new Color(0.85f, 0.9f, 0.8f, 1f);
            Fill(label, 40f);
            return label;
        }

        private RectTransform Row(Transform parent, params (string label, Func<DevGame, string> action)[] buttons)
        {
            RectTransform row = Rect("Row", parent);
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 70f;
            foreach (var (label, action) in buttons)
                Fill(MakeButton(row, label, ButtonColour, () => Run(action)));
            return row;
        }

        private Button MakeButton(Transform parent, string text, Color colour, UnityAction onClick)
        {
            RectTransform rt = Rect(text, parent);
            rt.gameObject.AddComponent<Image>().color = colour;
            var button = rt.gameObject.AddComponent<Button>();
            button.onClick.AddListener(onClick);
            TMP_Text label = Label(rt, text, 28f, TextAlignmentOptions.Center);
            Stretch(label.rectTransform);
            ButtonCount++;
            return button;
        }

        private static TMP_Text Label(Transform parent, string text, float size, TextAlignmentOptions alignment)
        {
            RectTransform rt = Rect("Text", parent);
            var label = rt.gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = size;
            label.color = Color.white;
            label.alignment = alignment;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return label;
        }

        private static void Fill(Component c, float height = 70f)
        {
            var element = c.gameObject.GetComponent<LayoutElement>();
            if (element == null) element = c.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.flexibleWidth = 1f;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void Place(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }
    }
}
