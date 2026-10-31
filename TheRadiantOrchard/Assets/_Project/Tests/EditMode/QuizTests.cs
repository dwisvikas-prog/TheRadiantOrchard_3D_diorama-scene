using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// The Wisdom Tree quiz (CLAUDE.md 5.8, ENGINEERING_GUIDE 9.1): ready every 25 heals, no repeats until all are seen,
    /// a right answer starts Golden Harvest, a wrong one gives a hint, and either uses the quiz up (agreed for W3·02).
    /// </summary>
    public class QuizTests
    {
        private sealed class TelemetryLog : ITelemetrySink
        {
            public readonly List<string> Lines = new List<string>();
            public void Log(string eventName, string details) => Lines.Add(eventName + ":" + details);
        }

        private GameConfig _config;
        private GameState _state;
        private EventBus _events;
        private BoostService _boosts;
        private HealService _heal;
        private QuizManager _quiz;
        private List<QuizReady> _ready;
        private List<QuizAnswered> _answered;
        private TelemetryLog _telemetry;
        private ITelemetrySink _previousSink;
        private int _dirty;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _telemetry = new TelemetryLog();
            _previousSink = Telemetry.Sink;
            Telemetry.Sink = _telemetry;
            Fresh(GameState.CreateNew(_config.Balance));
        }

        [TearDown]
        public void TearDown()
        {
            _quiz?.Dispose();
            Telemetry.Sink = _previousSink;
        }

        private void Fresh(GameState state)
        {
            _quiz?.Dispose();
            _state = state;
            _events = new EventBus();
            _ready = new List<QuizReady>();
            _answered = new List<QuizAnswered>();
            _events.Subscribe<QuizReady>(_ready.Add);
            _events.Subscribe<QuizAnswered>(_answered.Add);
            _dirty = 0;
            _boosts = new BoostService(_events);
            _heal = new HealService(_state, _config, _events, () => _dirty++, null, _boosts);
            _quiz = new QuizManager(_state, _config, _events, _boosts, () => _dirty++, new System.Random(7));
        }

        private int CorrectIndex(QuizScenario s) => s.CorrectIndex;
        private int WrongIndex(QuizScenario s) => (s.CorrectIndex + 1) % QuizScenario.OptionCount;

        [Test]
        public void TheQuizSetHasOneValidQuestionPerVirtue()
        {
            QuizSet set = _config.Quiz;
            Assert.IsNotNull(set, "GameConfig has its QuizSet (Radiant Orchard > Setup > Build Quiz).");
            Assert.AreEqual(9, set.Scenarios.Count);
            var ids = new HashSet<string>();
            foreach (QuizScenario s in set.Scenarios)
            {
                Assert.IsTrue(ids.Add(s.Id), $"'{s.Id}' once");
                Assert.IsNotNull(s.Virtue, s.Id);
                Assert.AreEqual(s.Virtue.Id, s.Id, "Id = virtue id.");
                Assert.AreEqual(QuizScenario.OptionCount, s.Options.Count, s.Id);
                CollectionAssert.AllItemsAreUnique(s.Options, $"{s.Id}: three different fruits");
                CollectionAssert.AllItemsAreNotNull(s.Options, s.Id);
                Assert.AreSame(s.Virtue, s.CorrectFruit.Virtue, $"{s.Id}: the right answer is the virtue's own fruit.");
                Assert.IsTrue(_config.Strings.Contains(s.ScenarioKey), s.ScenarioKey);
                Assert.IsTrue(_config.Strings.Contains(s.HintKey), s.HintKey);
            }
            foreach (VirtueDefinition v in _config.Virtues) Assert.IsTrue(ids.Contains(v.Id), $"{v.Id} has a question");
        }

        [Test]
        public void ReadyAtExactlyTwentyFiveHeals()
        {
            Assert.AreEqual(25, _config.Balance.QuizEveryHeals, "CLAUDE.md 5.8");
            for (int i = 0; i < 24; i++) _heal.Heal("strawberry");
            Assert.IsFalse(_quiz.IsReady);
            Assert.IsNull(_quiz.Open(), "Nothing to open yet.");
            Assert.AreEqual(0, _ready.Count);

            _heal.Heal("strawberry");
            Assert.IsTrue(_quiz.IsReady);
            Assert.AreEqual(1, _ready.Count);

            for (int i = 0; i < 5; i++) _heal.Heal("strawberry");
            Assert.AreEqual(1, _ready.Count, "Announced once while waiting.");
        }

        [Test]
        public void ClosingWithoutAnsweringKeepsTheSameQuestion()
        {
            _quiz.MakeReady();
            QuizScenario first = _quiz.Open();
            Assert.IsNotNull(first);
            Assert.AreSame(first, _quiz.Open());
            Assert.AreSame(first, _quiz.Open());
            Assert.IsTrue(_quiz.IsReady, "Still waiting.");
        }

        [Test]
        public void ARightAnswerStartsGoldenHarvestAndUsesTheQuizUp()
        {
            _quiz.MakeReady();
            QuizScenario s = _quiz.Open();
            QuizOutcome outcome = _quiz.Answer(CorrectIndex(s));

            Assert.IsTrue(outcome.Answered);
            Assert.IsTrue(outcome.Correct);
            Assert.IsNull(outcome.HintKey);
            Assert.IsTrue(_boosts.IsActive(BoostService.GoldenHarvest));
            Assert.AreEqual(60f, _boosts.SecondsLeft(BoostService.GoldenHarvest), 1e-3f, "CLAUDE.md 5.8: 60 s");
            Assert.AreEqual(2f, _boosts.SeedMultiplier, 1e-5f, "Double seeds.");
            Assert.AreEqual(20, _heal.Heal("strawberry"), "10 x 2 during Golden Harvest.");
            Assert.AreEqual(1, _state.HealedSinceQuiz, "The count started again (then one heal).");
            Assert.IsFalse(_quiz.IsReady);
            CollectionAssert.AreEqual(new[] { s.Id }, _state.QuizSeen);
            Assert.AreEqual(1, _answered.Count);
            Assert.IsTrue(_answered[0].Correct);
            CollectionAssert.Contains(_telemetry.Lines, $"quiz_answered:{s.Id}:right");
            Assert.Greater(_dirty, 0);
        }

        [Test]
        public void AWrongAnswerGivesTheHintWithNoPenaltyAndUsesTheQuizUp()
        {
            _state.Seeds = 100;
            _quiz.MakeReady();
            QuizScenario s = _quiz.Open();
            QuizOutcome outcome = _quiz.Answer(WrongIndex(s));

            Assert.IsTrue(outcome.Answered);
            Assert.IsFalse(outcome.Correct);
            Assert.AreEqual(s.HintKey, outcome.HintKey);
            Assert.IsFalse(_boosts.IsActive(BoostService.GoldenHarvest));
            Assert.AreEqual(100, _state.Seeds, "No penalty.");
            Assert.AreEqual(0, _state.HealedSinceQuiz);
            Assert.IsFalse(_quiz.IsReady, "No retry (agreed option a).");
            CollectionAssert.Contains(_state.QuizSeen, s.Id);
            CollectionAssert.Contains(_telemetry.Lines, $"quiz_answered:{s.Id}:wrong");
            Assert.AreEqual(QuizOutcome.None.Answered, _quiz.Answer(0).Answered, "Nothing open any more.");
        }

        [Test]
        public void NoRepeatsUntilAllNineAreSeenThenANewRoundNotStartingWithTheLast()
        {
            var order = new List<string>();
            for (int i = 0; i < 9; i++)
            {
                _quiz.MakeReady();
                QuizScenario s = _quiz.Open();
                order.Add(s.Id);
                _quiz.Answer(i % 2 == 0 ? CorrectIndex(s) : WrongIndex(s));
            }
            CollectionAssert.AllItemsAreUnique(order, "Nine different questions.");
            Assert.AreEqual(9, _state.QuizSeen.Count);

            for (int round = 0; round < 20; round++)
            {
                Fresh(GameState.CreateNew(_config.Balance));
                string last = order[order.Count - 1];
                _state.QuizSeen.AddRange(order);
                _quiz.MakeReady();
                QuizScenario next = _quiz.Open();
                Assert.AreNotEqual(last, next.Id, "A new round never opens with the question just answered.");
                _quiz.Answer(CorrectIndex(next));
                CollectionAssert.AreEqual(new[] { next.Id }, _state.QuizSeen, "The seen list starts again.");
            }
        }

        [Test]
        public void SeenQuestionsSurviveASaveAndAreNotAskedAgain()
        {
            _quiz.MakeReady();
            QuizScenario first = _quiz.Open();
            _quiz.Answer(CorrectIndex(first));

            GameState loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(_state));
            CollectionAssert.AreEqual(new[] { first.Id }, loaded.QuizSeen);
            for (int i = 0; i < 10; i++)
            {
                Fresh(JsonUtility.FromJson<GameState>(JsonUtility.ToJson(loaded)));
                _quiz.MakeReady();
                Assert.AreNotEqual(first.Id, _quiz.Open().Id);
            }
        }

        [Test]
        public void ALaunchPastTheCountStartsReadyWithoutAnAnnouncement()
        {
            GameState state = GameState.CreateNew(_config.Balance);
            state.HealedSinceQuiz = 30;
            Fresh(state);
            Assert.IsTrue(_quiz.IsReady, "The views read IsReady at start and light the tree.");
            _heal.Heal("strawberry");
            Assert.AreEqual(0, _ready.Count, "Already waiting; no second announcement.");
        }

        [Test]
        public void PickNextIsPureAndAvoidsTheLastOnANewRound()
        {
            var scenarios = new List<QuizScenario>
            {
                new QuizScenario("a", null, "k", "h", new FruitDefinition[3], 0),
                new QuizScenario("b", null, "k", "h", new FruitDefinition[3], 0),
            };
            var random = new System.Random(3);
            Assert.AreEqual("b", QuizRules.PickNext(scenarios, new[] { "a" }, random, out bool round).Id);
            Assert.IsFalse(round);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual("a", QuizRules.PickNext(scenarios, new[] { "a", "b" }, random, out round).Id);
                Assert.IsTrue(round);
            }
            Assert.IsNull(QuizRules.PickNext(new List<QuizScenario>(), new string[0], random, out _));
        }
    }
}
