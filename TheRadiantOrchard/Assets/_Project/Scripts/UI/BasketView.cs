using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The basket on the HUD (CLAUDE.md 5.5): harvested fruit nobody has asked for yet, one chip per fruit with its
    /// count, kept until a matching visitor comes. Updates only on BasketChanged. Tonics fly to and from Anchor.
    /// </summary>
    public sealed class BasketView : MonoBehaviour
    {
        public const string TitleKey = "ui.basket.title";
        public const string CountKey = "ui.basket.count";

        [SerializeField] private TMP_Text _title;
        [Tooltip("Where tonics land in and leave the basket.")]
        [SerializeField] private RectTransform _anchor;
        [SerializeField] private BasketChipView _chipTemplate;

        private readonly List<BasketChipView> _chips = new List<BasketChipView>(9);
        private EventBus _events;
        private StringTable _strings;
        private bool _subscribed;

        /// <summary>The basket's position in screen pixels (the HUD is a screen-space overlay).</summary>
        public Vector2 AnchorScreenPoint => _anchor.position;

        public void Init(GameConfig config, GameState state, EventBus events)
        {
            _events = events;
            _strings = config.Strings;
            _title.text = _strings.Get(TitleKey);
            _chipTemplate.gameObject.SetActive(false);
            for (int i = 0; i < config.Fruits.Count; i++)
            {
                BasketChipView chip = Instantiate(_chipTemplate, _chipTemplate.transform.parent);
                chip.name = "Chip_" + config.Fruits[i].Id;
                chip.Show(config.Fruits[i]);
                chip.SetCount(state.GetBasketCount(config.Fruits[i].Id), _strings.Get(CountKey));
                _chips.Add(chip);
            }
            Subscribe();
        }

        /// <summary>The chip shown for this fruit, or null (tests).</summary>
        public BasketChipView ChipFor(string fruitId)
        {
            for (int i = 0; i < _chips.Count; i++)
                if (_chips[i].FruitId == fruitId) return _chips[i];
            return null;
        }

        private void OnBasketChanged(BasketChanged e) => ChipFor(e.FruitId)?.SetCount(e.Count, _strings.Get(CountKey));

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<BasketChanged>(OnBasketChanged);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _events.Unsubscribe<BasketChanged>(OnBasketChanged);
            _subscribed = false;
        }

        private void Awake()
        {
            if (_title == null || _anchor == null || _chipTemplate == null)
                Log.Error($"BasketView '{name}': Title, Anchor and Chip Template must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
