using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RadiantOrchard.UI
{
    /// <summary>One fruit in the plant menu: icon, name, price; greyed with a padlock while locked.</summary>
    public sealed class FruitOptionView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _background;
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _price;
        [SerializeField] private GameObject _lock;
        [SerializeField] private TMP_Text _lockLabel;

        [SerializeField] private Color _availableBackground = new Color(1f, 1f, 1f, 1f);
        [SerializeField] private Color _unavailableBackground = new Color(0.82f, 0.80f, 0.76f, 1f);
        [SerializeField] private Color _priceColour = new Color(0.18f, 0.42f, 0.31f, 1f);
        [SerializeField] private Color _tooExpensiveColour = new Color(0.72f, 0.24f, 0.2f, 1f);

        private FruitDefinition _fruit;

        public FruitDefinition Fruit => _fruit;

        public void Bind(FruitDefinition fruit, StringTable strings, Action<FruitDefinition> onChosen)
        {
            _fruit = fruit;
            _name.text = strings.Get(fruit.DisplayNameKey);
            if (fruit.Icon != null)
            {
                _icon.sprite = fruit.Icon;
                _icon.color = Color.white;
            }
            else
            {
                // Placeholder until the fruit icons (ICON-01..09) exist.
                _icon.sprite = null;
                _icon.color = fruit.Tint;
            }
            _button.onClick.RemoveAllListeners();
            _button.onClick.AddListener(() => onChosen(_fruit));
        }

        public void Show(PlantResult state, StringTable strings)
        {
            bool locked = state == PlantResult.FruitLocked;
            bool available = state == PlantResult.Planted;

            // A locked fruit shows when it unlocks in place of its price, so the two lines never overlap.
            _lock.SetActive(locked);
            _price.gameObject.SetActive(!locked);
            if (locked) _lockLabel.text = string.Format(strings.Get("ui.plant.unlocks_at"), _fruit.UnlockStage);

            _price.text = _fruit.IsFree ? strings.Get("ui.plant.free") : string.Format(strings.Get("ui.plant.price"), _fruit.PlantCost);
            _price.color = state == PlantResult.NotEnoughSeeds ? _tooExpensiveColour : _priceColour;
            _background.color = available ? _availableBackground : _unavailableBackground;
            _icon.canvasRenderer.SetAlpha(available ? 1f : 0.45f);
            _button.interactable = available;
        }

        private void Awake()
        {
            if (_button == null || _background == null || _icon == null || _name == null || _price == null || _lock == null || _lockLabel == null)
                Log.Error($"FruitOptionView '{name}': a reference is missing; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
