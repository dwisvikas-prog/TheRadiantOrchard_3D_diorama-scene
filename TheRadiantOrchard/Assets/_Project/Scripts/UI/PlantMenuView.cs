using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The plant menu (CLAUDE.md 5.3): every fruit with its price; locked fruits greyed with a padlock. Choosing an
    /// available fruit plants it through PlotManager (which charges the seeds) and closes the menu.
    /// One option per fruit in GameConfig, created once from a template, so a new fruit needs no code.
    /// </summary>
    public sealed class PlantMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _seeds;
        [SerializeField] private TMP_Text _closeLabel;
        [SerializeField] private Button _close;
        [SerializeField] private Button _backdrop;
        [SerializeField] private Transform _optionsParent;
        [SerializeField] private FruitOptionView _optionTemplate;

        private readonly List<FruitOptionView> _options = new List<FruitOptionView>();
        private PlotManager _plots;
        private GameConfig _config;
        private GameState _state;
        private EventBus _events;
        private int _plot = -1;

        public bool IsOpen => _root.activeSelf;

        public void Init(PlotManager plots, GameConfig config, GameState state, EventBus events)
        {
            _plots = plots;
            _config = config;
            _state = state;
            _events = events;

            StringTable strings = config.Strings;
            _title.text = strings.Get("ui.plant.title");
            _closeLabel.text = strings.Get("ui.close");
            _close.onClick.AddListener(Hide);
            _backdrop.onClick.AddListener(Hide);

            _optionTemplate.gameObject.SetActive(false);
            foreach (FruitDefinition fruit in config.Fruits)
            {
                FruitOptionView option = Instantiate(_optionTemplate, _optionsParent);
                option.name = "Option_" + fruit.Id;
                option.gameObject.SetActive(true);
                option.Bind(fruit, strings, Choose);
                _options.Add(option);
            }
            _root.SetActive(false);
        }

        public void Show(int plot)
        {
            _plot = plot;
            _root.SetActive(true);
            Refresh();
            _events.Subscribe<SeedsChanged>(OnSeedsChanged);
        }

        public void Hide()
        {
            if (!_root.activeSelf) return;
            _root.SetActive(false);
            _plot = -1;
            _events.Unsubscribe<SeedsChanged>(OnSeedsChanged);
        }

        private void OnDisable()
        {
            if (_events != null) _events.Unsubscribe<SeedsChanged>(OnSeedsChanged);
        }

        private void OnSeedsChanged(SeedsChanged e) => Refresh();

        private void Refresh()
        {
            StringTable strings = _config.Strings;
            _seeds.text = string.Format(strings.Get("ui.plant.seeds"), _state.Seeds);
            foreach (FruitOptionView option in _options)
                option.Show(_plots.CanPlant(_plot, option.Fruit), strings);
        }

        private void Choose(FruitDefinition fruit)
        {
            PlantResult result = _plots.TryPlant(_plot, fruit.Id);
            if (result == PlantResult.Planted) Hide();
            else Refresh();
        }

        private void Awake()
        {
            if (_root == null || _title == null || _seeds == null || _closeLabel == null || _close == null || _backdrop == null || _optionsParent == null || _optionTemplate == null)
                Log.Error($"PlantMenuView '{name}': a reference is missing; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
