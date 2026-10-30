using RadiantOrchard.Core;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The always-on HUD. For now the seed counter (CLAUDE.md 3: seeds are the only currency); the text only changes
    /// on SeedsChanged, never per frame. The progress bar joins it in Week 2.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        public const string SeedsKey = "ui.hud.seeds";

        [SerializeField] private TMP_Text _seeds;

        private EventBus _events;
        private StringTable _strings;
        private bool _subscribed;

        public void Init(GameState state, EventBus events, StringTable strings)
        {
            _events = events;
            _strings = strings;
            ShowSeeds(state.Seeds);
            Subscribe();
        }

        private void OnSeedsChanged(SeedsChanged e) => ShowSeeds(e.Seeds);

        private void ShowSeeds(int seeds) => _seeds.text = string.Format(_strings.Get(SeedsKey), seeds);

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<SeedsChanged>(OnSeedsChanged);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _events.Unsubscribe<SeedsChanged>(OnSeedsChanged);
            _subscribed = false;
        }

        private void Awake()
        {
            if (_seeds == null)
                Log.Error($"HudView '{name}': Seeds text must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
