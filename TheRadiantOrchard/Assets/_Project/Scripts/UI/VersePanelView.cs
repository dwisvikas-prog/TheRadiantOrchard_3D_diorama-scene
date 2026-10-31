using RadiantOrchard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The verse card shown during the bloom celebration (CLAUDE.md 5.9, UI-10): a title, the verse (Galatians 5:22-23,
    /// placeholder until the client picks a translation) and its reference, all from the StringTable. A tap anywhere
    /// closes it, but only after a short moment so a stray tap does not skip it. Fades in and out; Update only runs while
    /// it is open or fading.
    /// </summary>
    public sealed class VersePanelView : MonoBehaviour
    {
        public const string TitleKey = "ui.bloom.title";
        public const string TreeNameKey = "tree.wisdom.name";
        public const string VerseKey = "bloom.verse.text";
        public const string ReferenceKey = "bloom.verse.reference";
        public const string ContinueKey = "ui.bloom.continue";

        [SerializeField] private GameObject _root;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Button _backdrop;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _verse;
        [SerializeField] private TMP_Text _reference;
        [SerializeField] private TMP_Text _continue;
        [SerializeField] private float _fadeSeconds = 0.4f;
        [Tooltip("Taps before this are ignored, so the verse is always seen.")]
        [SerializeField] private float _minSeconds = 1.5f;

        private float _target;
        private float _alpha;
        private float _shownFor;

        /// <summary>Open and not yet closing.</summary>
        public bool IsOpen => _root.activeSelf && _target > 0f;
        public string Title => _title.text;
        public string Verse => _verse.text;
        public string Reference => _reference.text;

        public void Init(StringTable strings)
        {
            _title.text = string.Format(strings.Get(TitleKey), strings.Get(TreeNameKey));
            _verse.text = strings.Get(VerseKey);
            _reference.text = strings.Get(ReferenceKey);
            _continue.text = strings.Get(ContinueKey);
            _backdrop.onClick.RemoveListener(OnTapped);
            _backdrop.onClick.AddListener(OnTapped);
            _alpha = _target = 0f;
            _group.alpha = 0f;
            _root.SetActive(false);
        }

        public void Show()
        {
            _root.SetActive(true);
            _target = 1f;
            _shownFor = 0f;
        }

        public void Hide() => _target = 0f;

        private void OnTapped()
        {
            if (_shownFor >= _minSeconds) Hide();
        }

        private void Update()
        {
            if (!_root.activeSelf) return;
            _shownFor += Time.deltaTime;
            if (Mathf.Approximately(_alpha, _target)) return;
            _alpha = Mathf.MoveTowards(_alpha, _target, Time.deltaTime / Mathf.Max(0.01f, _fadeSeconds));
            _group.alpha = _alpha;
            if (_alpha <= 0f) _root.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_backdrop != null) _backdrop.onClick.RemoveListener(OnTapped);
        }

        private void Awake()
        {
            if (_root == null || _group == null || _backdrop == null || _title == null || _verse == null || _reference == null || _continue == null)
                Log.Error($"VersePanelView '{name}': Root, Group, Backdrop, Title, Verse, Reference and Continue must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
