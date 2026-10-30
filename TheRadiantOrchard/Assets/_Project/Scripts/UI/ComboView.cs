using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The small combo counter (CLAUDE.md 5.13): "Combo ×N" appears from the second quick heal, pops on each step and
    /// goes once the combo window has passed with no heal. Text changes only on a heal; Update only runs the pop and the
    /// countdown while it is showing.
    /// </summary>
    public sealed class ComboView : MonoBehaviour
    {
        public const string Key = "ui.combo";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _text;
        [SerializeField] private float _popScale = 1.3f;
        [SerializeField] private float _popSeconds = 0.25f;

        private EventBus _events;
        private StringTable _strings;
        private float _windowSeconds;
        private float _shownFor = -1f;
        private bool _subscribed;

        public bool IsShowing => _root.activeSelf;
        public string Text => _text.text;

        public void Init(GameConfig config, EventBus events)
        {
            _events = events;
            _strings = config.Strings;
            _windowSeconds = config.Balance.ComboWindowSeconds;
            Hide();
            Subscribe();
        }

        private void OnHealed(VisitorHealed e)
        {
            if (e.Combo < 2) return;
            _text.text = string.Format(_strings.Get(Key), e.Combo);
            _root.SetActive(true);
            _shownFor = 0f;
        }

        private void Update()
        {
            if (_shownFor < 0f) return;
            _shownFor += Time.deltaTime;
            if (_shownFor >= _windowSeconds)
            {
                Hide();
                return;
            }
            float pop = _shownFor < _popSeconds ? Mathf.Sin(_shownFor / _popSeconds * Mathf.PI) : 0f;
            _root.transform.localScale = Vector3.one * Mathf.Lerp(1f, _popScale, pop);
        }

        private void Hide()
        {
            _shownFor = -1f;
            _root.transform.localScale = Vector3.one;
            _root.SetActive(false);
        }

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<VisitorHealed>(OnHealed);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _events.Unsubscribe<VisitorHealed>(OnHealed);
            _subscribed = false;
        }

        private void Awake()
        {
            if (_root == null || _text == null)
                Log.Error($"ComboView '{name}': Root and Text must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
