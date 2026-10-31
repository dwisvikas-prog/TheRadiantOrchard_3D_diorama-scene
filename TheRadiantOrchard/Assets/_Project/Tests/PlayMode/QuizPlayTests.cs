using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The Wisdom Tree quiz in the running game (CLAUDE.md 5.8): dev "Quiz now" lights the tree, the sheet opens with
    /// the question, a right answer turns on Golden Harvest (glow, timer, double seeds), a wrong one shows the hint and
    /// closes. Saves go to a temporary folder.
    /// </summary>
    public class QuizPlayTests
    {
        private string _saveDir;
        private DevGame _game;
        private QuizPanelView _panel;
        private WisdomTreeView _tree;
        private GoldenHarvestView _golden;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardQuizTest", Guid.NewGuid().ToString("N"));
            yield return TestGame.Boot(_saveDir);
            _game = DevGame.Current;
            Assert.IsNotNull(_game?.Quiz, "GameInstaller did not finish wiring.");
            // Test code may search the scene; the no-Find rule is for runtime code.
            _panel = Object.FindFirstObjectByType<QuizPanelView>();
            _tree = Object.FindFirstObjectByType<WisdomTreeView>();
            _golden = Object.FindFirstObjectByType<GoldenHarvestView>();
            Assert.IsNotNull(_panel, "No QuizPanelView (Radiant Orchard > Setup > Build UI).");
            Assert.IsNotNull(_tree, "No WisdomTreeView (Radiant Orchard > Setup > Build Placeholder Farm).");
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        private int ChoiceFor(FruitDefinition fruit)
        {
            for (int i = 0; i < QuizScenario.OptionCount; i++)
                if (_panel.OptionAt(i) == fruit) return i;
            return -1;
        }

        private IEnumerator OpenQuiz()
        {
            Assert.IsFalse(_tree.IsGlowing, "No quiz yet.");
            Assert.IsFalse(_panel.Show(), "Nothing to open yet.");
            DevActions.QuizNow(_game);
            yield return null;
            Assert.IsTrue(_tree.IsGlowing, "The tree glows while a quiz waits.");
            // Test code may search the scene. The glow must also be visible: wider than the trunk, not shrunk to nothing.
            var glow = _tree.transform.Find("QuizGlow").GetComponent<SpriteRenderer>();
            Assert.Greater(glow.bounds.size.x, 2.5f, "The glow surrounds the tree.");
            Assert.Greater(glow.color.a, 0.3f);
            Assert.IsTrue(_tree.Contains(_tree.transform.position + Vector3.up * 2f), "A tap on the trunk hits the tree.");

            Assert.IsTrue(_panel.Show(), "A tap on the tree opens the sheet.");
            Assert.IsTrue(_panel.IsOpen);
            Assert.IsFalse(string.IsNullOrEmpty(_panel.ScenarioText));
            for (int i = 0; i < QuizScenario.OptionCount; i++) Assert.IsNotNull(_panel.OptionAt(i));
        }

        [UnityTest]
        public IEnumerator ARightAnswerTurnsOnGoldenHarvest()
        {
            yield return OpenQuiz();
            QuizScenario scenario = _game.Quiz.Open();
            int seeds = _game.State.Seeds;

            _panel.Choose(ChoiceFor(scenario.CorrectFruit));
            yield return null;

            Assert.IsFalse(_panel.IsOpen, "A right answer closes the sheet.");
            Assert.IsFalse(_tree.IsGlowing);
            Assert.IsTrue(_game.Boosts.IsActive(BoostService.GoldenHarvest));
            Assert.IsTrue(_golden.IsShowing, "Gold edge glow and timer.");
            StringAssert.StartsWith("Double seeds ", _golden.TimerText);
            StringAssert.Contains(":", _golden.TimerText);

            DevActions.Heal(_game, 1);
            Assert.AreEqual(seeds + 20, _game.State.Seeds, "Double seeds: 10 x 2.");
        }

        [UnityTest]
        public IEnumerator AWrongAnswerShowsTheHintThenCloses()
        {
            yield return OpenQuiz();
            QuizScenario scenario = _game.Quiz.Open();
            int wrong = (ChoiceFor(scenario.CorrectFruit) + 1) % QuizScenario.OptionCount;

            _panel.Choose(wrong);
            Assert.IsTrue(_panel.IsOpen, "Stays open to show the hint.");
            Assert.IsTrue(_panel.IsShowingHint);
            Assert.AreEqual(_game.Config.Strings.Get(scenario.HintKey), _panel.HintText);
            Assert.IsFalse(_game.Boosts.IsActive(BoostService.GoldenHarvest), "No Golden Harvest.");
            Assert.IsFalse(_tree.IsGlowing, "The quiz is used up.");

            yield return new WaitForSeconds(3.3f);
            Assert.IsFalse(_panel.IsOpen, "Closes by itself after the hint.");
            Assert.IsFalse(_golden.IsShowing);
        }
    }
}
