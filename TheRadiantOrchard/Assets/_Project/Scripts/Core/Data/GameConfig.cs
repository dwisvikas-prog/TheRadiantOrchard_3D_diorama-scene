using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// The one config asset Boot loads: references every content and tuning asset.
    /// Grows as systems are added (StringTable, QuizSet, MiniEventDefinitions...).
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Game Config", fileName = "GameConfig")]
    public sealed class GameConfig : ScriptableObject
    {
        [SerializeField] private BalanceConfig _balance;
        [Tooltip("All fruits, in the order the plant menu shows them.")]
        [SerializeField] private FruitDefinition[] _fruits = new FruitDefinition[0];

        public BalanceConfig Balance => _balance;
        public IReadOnlyList<FruitDefinition> Fruits => _fruits;

        /// <summary>Fruit with the given save-file id, or null if none.</summary>
        public FruitDefinition FindFruit(string id)
        {
            for (int i = 0; i < _fruits.Length; i++)
                if (_fruits[i] != null && _fruits[i].Id == id)
                    return _fruits[i];
            return null;
        }

        private void OnValidate()
        {
            if (_balance == null)
                Debug.LogError($"{name}: Balance is not assigned.", this);

            var seen = new HashSet<string>();
            for (int i = 0; i < _fruits.Length; i++)
            {
                if (_fruits[i] == null) { Debug.LogError($"{name}: Fruits[{i}] is empty.", this); continue; }
                if (!seen.Add(_fruits[i].Id)) Debug.LogError($"{name}: fruit id '{_fruits[i].Id}' is listed twice.", this);
            }
        }
    }
}
