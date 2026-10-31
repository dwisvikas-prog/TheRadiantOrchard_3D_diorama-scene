using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The Vibrancy Meter on the HUD (CLAUDE.md 5.7): the stage reached, a bar filling towards the next stage and the
    /// heals made of those needed; "Farm restored" once stage 5 is reached. Redrawn only on VibrancyChanged.
    /// </summary>
    public sealed class VibrancyMeterView : MonoBehaviour
    {
        public const string StageKey = "ui.vibrancy.stage";
        public const string ProgressKey = "ui.vibrancy.progress";
        public const string CompleteKey = "ui.vibrancy.complete";

        [SerializeField] private TMP_Text _stage;
        [SerializeField] private TMP_Text _count;
        [Tooltip("Stretches from the left of the bar; its right anchor is set to the fill fraction.")]
        [SerializeField] private RectTransform _fill;

        private GameState _state;
        private BalanceConfig _balance;
        private StringTable _strings;
        private EventBus _events;
        private bool _subscribed;

        public string StageText => _stage.text;
        public string CountText => _count.text;
        public float Fill => _fill.anchorMax.x;

        public void Init(GameConfig config, GameState state, EventBus events)
        {
            _state = state;
            _balance = config.Balance;
            _strings = config.Strings;
            _events = events;
            Redraw();
            Subscribe();
        }

        private void OnChanged(VibrancyChanged e) => Redraw();

        private void Redraw()
        {
            VibrancyProgress progress = VibrancyRules.Progress(_balance, _state.HealedTotal, _state.VibrancyStage);
            _stage.text = string.Format(_strings.Get(StageKey), progress.Stage);
            _count.text = progress.IsComplete
                ? _strings.Get(CompleteKey)
                : string.Format(_strings.Get(ProgressKey), progress.Done, progress.Needed);
            _fill.anchorMax = new Vector2(progress.Fraction, 1f);
        }

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<VibrancyChanged>(OnChanged);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _events.Unsubscribe<VibrancyChanged>(OnChanged);
            _subscribed = false;
        }

        private void Awake()
        {
            if (_stage == null || _count == null || _fill == null)
                Log.Error($"VibrancyMeterView '{name}': Stage, Count and Fill must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
