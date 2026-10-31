using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// Golden Harvest on screen (CLAUDE.md 5.8, FX-12 placeholder): a warm gold glow around the screen edges (one sliced
    /// image with no centre, so it adds no full-screen overdraw) and a "Golden Harvest 0:42" pill. Lives on its own
    /// Canvas so its pulse never rebuilds the HUD; the text changes once a second.
    /// </summary>
    public sealed class GoldenHarvestView : MonoBehaviour
    {
        public const string TimerKey = "ui.golden_harvest.timer";

        [SerializeField] private GameObject _root;
        [Tooltip("Pulses the edge glow without rebuilding its mesh.")]
        [SerializeField] private CanvasGroup _glow;
        [SerializeField] private TMP_Text _timer;
        [SerializeField] private float _pulseSeconds = 1.4f;
        [Range(0f, 1f)] [SerializeField] private float _minAlpha = 0.45f;
        [Range(0f, 1f)] [SerializeField] private float _maxAlpha = 0.9f;

        private BoostService _boosts;
        private StringTable _strings;
        private EventBus _events;
        private bool _subscribed;
        private int _shownSecond = -1;
        private float _time;

        public bool IsShowing => _root.activeSelf;
        public string TimerText => _timer.text;

        public void Init(GameConfig config, BoostService boosts, EventBus events)
        {
            _strings = config.Strings;
            _boosts = boosts;
            _events = events;
            SetShown(boosts.IsActive(BoostService.GoldenHarvest));
            Subscribe();
        }

        private void OnStarted(BoostStarted e)
        {
            if (e.Id == BoostService.GoldenHarvest) SetShown(true);
        }

        private void OnEnded(BoostEnded e)
        {
            if (e.Id == BoostService.GoldenHarvest) SetShown(false);
        }

        private void SetShown(bool shown)
        {
            _root.SetActive(shown);
            _shownSecond = -1;
            _time = 0f;
            if (shown) Refresh();
        }

        private void Update()
        {
            if (_boosts == null || !_root.activeSelf) return;
            _time += Time.deltaTime;
            Refresh();
        }

        private void Refresh()
        {
            float k = 0.5f - 0.5f * Mathf.Cos(_time / Mathf.Max(0.01f, _pulseSeconds) * Mathf.PI * 2f);
            _glow.alpha = Mathf.Lerp(_minAlpha, _maxAlpha, k);

            int second = Mathf.CeilToInt(_boosts.SecondsLeft(BoostService.GoldenHarvest));
            if (second == _shownSecond) return;
            _shownSecond = second;
            _timer.text = string.Format(_strings.Get(TimerKey), GrowthTimerText.Format(second, _strings));
        }

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<BoostStarted>(OnStarted);
            _events.Subscribe<BoostEnded>(OnEnded);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _events.Unsubscribe<BoostStarted>(OnStarted);
            _events.Unsubscribe<BoostEnded>(OnEnded);
            _subscribed = false;
        }

        private void Awake()
        {
            if (_root == null || _glow == null || _timer == null)
                Log.Error($"GoldenHarvestView '{name}': Root, Glow and Timer must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
