using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The Wisdom Tree's bottom sheet (CLAUDE.md 5.8): the scenario and three fruit choices in a shuffled order. A right
    /// answer closes it (Golden Harvest starts in QuizManager); a wrong one greys the choices, shows a gentle hint and
    /// closes after a few seconds or on a tap. Closing without answering keeps the question for the next tap.
    /// </summary>
    public sealed class QuizPanelView : MonoBehaviour
    {
        public const string TitleKey = "ui.quiz.title";

        [SerializeField] private GameObject _root;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _scenario;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private Button _close;
        [SerializeField] private TMP_Text _closeLabel;
        [SerializeField] private Button _backdrop;
        [SerializeField] private Button[] _optionButtons = new Button[QuizScenario.OptionCount];
        [SerializeField] private Image[] _optionIcons = new Image[QuizScenario.OptionCount];
        [SerializeField] private TMP_Text[] _optionNames = new TMP_Text[QuizScenario.OptionCount];
        [Tooltip("How long the hint stays before the sheet closes by itself.")]
        [Min(0f)] [SerializeField] private float _hintSeconds = 3f;

        private readonly int[] _order = new int[QuizScenario.OptionCount];
        private QuizManager _quiz;
        private StringTable _strings;
        private QuizScenario _shown;
        private float _hintLeft = -1f;

        public bool IsOpen => _root.activeSelf;
        public bool IsShowingHint => _hintLeft >= 0f;
        public string ScenarioText => _scenario.text;
        public string HintText => _hint.text;

        /// <summary>The fruit shown on choice button i (0..2), for tests.</summary>
        public FruitDefinition OptionAt(int i) => _shown != null ? _shown.Options[_order[i]] : null;

        public void Init(QuizManager quiz, GameConfig config)
        {
            _quiz = quiz;
            _strings = config.Strings;
            _title.text = _strings.Get(TitleKey);
            _closeLabel.text = _strings.Get("ui.close");
            _close.onClick.AddListener(Hide);
            _backdrop.onClick.AddListener(Hide);
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                int index = i;
                _optionButtons[i].onClick.AddListener(() => Choose(index));
            }
            _root.SetActive(false);
        }

        /// <summary>Opens the waiting question. False when no quiz is ready.</summary>
        public bool Show()
        {
            QuizScenario scenario = _quiz.Open();
            if (scenario == null) return false;

            if (scenario != _shown) Shuffle();
            _shown = scenario;
            _scenario.text = _strings.Get(scenario.ScenarioKey);
            for (int i = 0; i < _optionButtons.Length; i++)
            {
                FruitDefinition fruit = scenario.Options[_order[i]];
                _optionNames[i].text = fruit != null ? _strings.Get(fruit.DisplayNameKey) : string.Empty;
                _optionIcons[i].sprite = fruit != null ? fruit.Icon : null;
                _optionIcons[i].color = fruit == null ? Color.clear : fruit.Icon != null ? Color.white : fruit.Tint;
                _optionButtons[i].interactable = true;
            }
            _hint.gameObject.SetActive(false);
            _hintLeft = -1f;
            _root.SetActive(true);
            return true;
        }

        public void Hide()
        {
            _hintLeft = -1f;
            _root.SetActive(false);
        }

        /// <summary>Answers with choice button i (0..2), as a tap on it does.</summary>
        public void Choose(int i)
        {
            if (!IsOpen || IsShowingHint || _shown == null) return;
            QuizOutcome outcome = _quiz.Answer(_order[i]);
            if (!outcome.Answered || outcome.Correct)
            {
                Hide();
                return;
            }
            foreach (Button b in _optionButtons) b.interactable = false;
            _hint.text = _strings.Get(outcome.HintKey);
            _hint.gameObject.SetActive(true);
            _hintLeft = _hintSeconds;
        }

        private void Update()
        {
            if (_hintLeft < 0f) return;
            _hintLeft -= Time.deltaTime;
            if (_hintLeft < 0f) Hide();
        }

        // A new question gets a fresh order, so the right answer is not always in the same place.
        private void Shuffle()
        {
            for (int i = 0; i < _order.Length; i++) _order[i] = i;
            for (int i = _order.Length - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }
        }

        private void Awake()
        {
            if (_root == null || _title == null || _scenario == null || _hint == null || _close == null || _closeLabel == null
                || _backdrop == null || _optionButtons.Length != QuizScenario.OptionCount)
                Log.Error($"QuizPanelView '{name}': a reference is missing; rebuild the UI (Radiant Orchard > Setup > Build UI).", this);
        }
    }
}
