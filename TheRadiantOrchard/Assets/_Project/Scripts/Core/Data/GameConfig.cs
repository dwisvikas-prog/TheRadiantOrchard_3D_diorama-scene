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
        [SerializeField] private StringTable _strings;
        [SerializeField] private FarmDefinition _farm;
        [Tooltip("All fruits, in the order the plant menu shows them.")]
        [SerializeField] private FruitDefinition[] _fruits = new FruitDefinition[0];
        [Tooltip("The nine virtues, in the order of CLAUDE.md section 4. Each fruit links to its own.")]
        [SerializeField] private VirtueDefinition[] _virtues = new VirtueDefinition[0];
        [Tooltip("Visitor kinds. Phase 1 has one; Phase 2 adds special visitors.")]
        [SerializeField] private VisitorDefinition[] _visitors = new VisitorDefinition[0];

        public BalanceConfig Balance => _balance;
        public StringTable Strings => _strings;
        public FarmDefinition Farm => _farm;
        public IReadOnlyList<FruitDefinition> Fruits => _fruits;
        public IReadOnlyList<VirtueDefinition> Virtues => _virtues;
        public IReadOnlyList<VisitorDefinition> Visitors => _visitors;

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
            if (_strings == null)
                Debug.LogError($"{name}: Strings is not assigned.", this);
            if (_farm == null)
                Debug.LogError($"{name}: Farm is not assigned.", this);

            if (_visitors.Length == 0)
                Debug.LogError($"{name}: at least one Visitor definition is needed.", this);

            var seen = new HashSet<string>();
            for (int i = 0; i < _fruits.Length; i++)
            {
                if (_fruits[i] == null) { Debug.LogError($"{name}: Fruits[{i}] is empty.", this); continue; }
                if (!seen.Add(_fruits[i].Id)) Debug.LogError($"{name}: fruit id '{_fruits[i].Id}' is listed twice.", this);
            }
        }
    }
}
