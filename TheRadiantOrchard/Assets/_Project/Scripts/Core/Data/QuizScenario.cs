using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// One Wisdom Tree question (CLAUDE.md 5.8): a short real-life scenario, three fruit choices with the right one
    /// marked, and a gentle hint shown after a wrong answer. Text is StringTable keys; the id is the scenario's virtue
    /// id, used in the save (quizSeen).
    /// </summary>
    [Serializable]
    public sealed class QuizScenario
    {
        public const int OptionCount = 3;

        [Tooltip("Stable id saved in quizSeen; the virtue's id (e.g. 'peace').")]
        [SerializeField] private string _id;
        [SerializeField] private VirtueDefinition _virtue;
        [SerializeField] private string _scenarioKey;
        [SerializeField] private string _hintKey;
        [Tooltip("The three fruits the player chooses from.")]
        [SerializeField] private FruitDefinition[] _options = new FruitDefinition[OptionCount];
        [Range(0, OptionCount - 1)] [SerializeField] private int _correctIndex;

        public QuizScenario() { }

        public QuizScenario(string id, VirtueDefinition virtue, string scenarioKey, string hintKey, FruitDefinition[] options, int correctIndex)
        {
            _id = id;
            _virtue = virtue;
            _scenarioKey = scenarioKey;
            _hintKey = hintKey;
            _options = options;
            _correctIndex = correctIndex;
        }

        public string Id => _id;
        public VirtueDefinition Virtue => _virtue;
        public string ScenarioKey => _scenarioKey;
        public string HintKey => _hintKey;
        public IReadOnlyList<FruitDefinition> Options => _options;
        public int CorrectIndex => _correctIndex;
        public FruitDefinition CorrectFruit => _correctIndex >= 0 && _correctIndex < _options.Length ? _options[_correctIndex] : null;
    }
}
