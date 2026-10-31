using System.Text;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The banner of the zone unlock sequence (CLAUDE.md 5.7): "Stage N!" and what the stage opened (new fruits and
    /// plots, or the zone / whole orchard restored). Pops in and fades out; the sequence decides how long it stays.
    /// Update only runs the fade while it is changing.
    /// </summary>
    public sealed class ZoneBannerView : MonoBehaviour
    {
        public const string TitleKey = "ui.banner.title";
        public const string FruitsKey = "ui.banner.fruits";
        public const string PlotsKey = "ui.banner.plots";
        public const string ZoneKey = "ui.banner.zone";
        public const string CompleteKey = "ui.banner.complete";

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _body;
        [SerializeField] private float _fadeSeconds = 0.25f;
        [SerializeField] private float _popScale = 1.15f;

        private StringTable _strings;
        private float _target;
        private float _alpha;

        public bool IsShowing => _root.activeSelf;
        public string Title => _title.text;
        public string Body => _body.text;

        public void Init(StringTable strings)
        {
            _strings = strings;
            _alpha = _target = 0f;
            Apply();
            _root.SetActive(false);
        }

        public void Show(StageUnlocks unlocks)
        {
            _title.text = string.Format(_strings.Get(TitleKey), unlocks.Stage);
            _body.text = BodyFor(unlocks, _strings);
            _root.SetActive(true);
            _target = 1f;
        }

        public void Hide() => _target = 0f;

        /// <summary>What the stage opened, one line per kind; the zone or whole orchard when nothing new can be planted.</summary>
        public static string BodyFor(StageUnlocks unlocks, StringTable strings)
        {
            if (unlocks.IsFinal) return strings.Get(CompleteKey);
            var body = new StringBuilder();
            if (unlocks.Fruits.Count > 0)
            {
                var names = new StringBuilder();
                for (int i = 0; i < unlocks.Fruits.Count; i++)
                {
                    if (i > 0) names.Append(", ");
                    names.Append(strings.Get(unlocks.Fruits[i].DisplayNameKey));
                }
                body.Append(string.Format(strings.Get(FruitsKey), names));
            }
            if (unlocks.NewPlots > 0)
            {
                if (body.Length > 0) body.Append('\n');
                body.Append(string.Format(strings.Get(PlotsKey), unlocks.NewPlots));
            }
            return body.Length > 0 ? body.ToString() : strings.Get(ZoneKey);
        }

        private void Update()
        {
            if (Mathf.Approximately(_alpha, _target)) return;
            _alpha = Mathf.MoveTowards(_alpha, _target, Time.deltaTime / Mathf.Max(0.01f, _fadeSeconds));
            Apply();
            if (_alpha <= 0f) _root.SetActive(false);
        }

        // Grows from a little larger as it fades in, so it pops into place.
        private void Apply()
        {
            _group.alpha = _alpha;
            _root.transform.localScale = Vector3.one * Mathf.Lerp(_popScale, 1f, _alpha);
        }

        private void Awake()
        {
            if (_root == null || _group == null || _title == null || _body == null)
                Log.Error($"ZoneBannerView '{name}': Root, Group, Title and Body must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
