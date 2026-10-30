using RadiantOrchard.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RadiantOrchard.UI
{
    /// <summary>One fruit in the HUD basket: its icon (or tint until the art exists) and how many are waiting there.</summary>
    public sealed class BasketChipView : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private TMP_Text _count;

        public string FruitId { get; private set; }

        public void Show(FruitDefinition fruit)
        {
            FruitId = fruit.Id;
            if (fruit.Icon != null)
            {
                _icon.sprite = fruit.Icon;
                _icon.color = Color.white;
            }
            else
            {
                _icon.sprite = null;
                _icon.color = fruit.Tint;
            }
        }

        /// <summary>Hidden at 0. Builds the text only when called, which is only when the count changes.</summary>
        public void SetCount(int count, string pattern)
        {
            gameObject.SetActive(count > 0);
            if (count > 0) _count.text = string.Format(pattern, count);
        }

        private void Awake()
        {
            if (_icon == null || _count == null)
                Log.Error($"BasketChipView '{name}': Icon and Count must be assigned; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
