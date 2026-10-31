using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// The Wisdom Tree's questions (CLAUDE.md 5.8): one scenario per virtue, client text (placeholders until then).
    /// Adding or changing a question is data only. Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Quiz Set", fileName = "QuizSet")]
    public sealed class QuizSet : ScriptableObject
    {
        [SerializeField] private QuizScenario[] _scenarios = new QuizScenario[0];

        public IReadOnlyList<QuizScenario> Scenarios => _scenarios;

        public QuizScenario Find(string id)
        {
            for (int i = 0; i < _scenarios.Length; i++)
                if (_scenarios[i] != null && _scenarios[i].Id == id) return _scenarios[i];
            return null;
        }

        private void OnValidate()
        {
            var ids = new HashSet<string>();
            for (int i = 0; i < _scenarios.Length; i++)
            {
                QuizScenario s = _scenarios[i];
                if (s == null) continue;
                if (string.IsNullOrWhiteSpace(s.Id)) Debug.LogError($"{name}: scenario {i} has no id.", this);
                else if (!ids.Add(s.Id)) Debug.LogError($"{name}: scenario id '{s.Id}' is used twice.", this);
                if (s.Options.Count != QuizScenario.OptionCount)
                    Debug.LogError($"{name}: scenario '{s.Id}' needs {QuizScenario.OptionCount} options.", this);
                if (s.CorrectFruit == null) Debug.LogError($"{name}: scenario '{s.Id}' has no correct option.", this);
            }
        }
    }
}
