using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Healing (CLAUDE.md 5.6): seeds, heal counters, events, and only the requested fruit heals.</summary>
    public class HealServiceTests
    {
        private GameConfig _config;
        private GameState _state;
        private EventBus _events;
        private int _dirty;
        private HealService _heal;
        private List<SeedsChanged> _seedEvents;
        private List<VisitorHealed> _healEvents;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _state = GameState.CreateNew(_config.Balance);
            _events = new EventBus();
            _dirty = 0;
            _seedEvents = new List<SeedsChanged>();
            _healEvents = new List<VisitorHealed>();
            _events.Subscribe<SeedsChanged>(_seedEvents.Add);
            _events.Subscribe<VisitorHealed>(_healEvents.Add);
            _heal = new HealService(_state, _config, _events, () => _dirty++);
        }

        [Test]
        public void AHealPaysSeedsAndCountsTowardsEverything()
        {
            int seeds = _heal.Heal("strawberry");

            Assert.AreEqual(_config.Balance.SeedsPerHeal, seeds);
            Assert.AreEqual(10, seeds, "CLAUDE.md 5.6: 10 seeds per heal.");
            Assert.AreEqual(10, _state.Seeds);
            Assert.AreEqual(1, _state.HealedTotal, "Progress bar +1.");
            Assert.AreEqual(1, _state.HealedSinceQuiz);
            Assert.AreEqual(1, _state.HealedSinceEvent);
            Assert.Greater(_dirty, 0, "A heal must be saved.");
        }

        [Test]
        public void AHealIsAnnouncedWithTheVirtue()
        {
            _heal.Heal("watermelon");

            Assert.AreEqual(1, _seedEvents.Count);
            Assert.AreEqual(10, _seedEvents[0].Seeds);
            Assert.AreEqual(10, _seedEvents[0].Delta);
            Assert.AreEqual(1, _healEvents.Count);
            Assert.AreEqual("watermelon", _healEvents[0].FruitId);
            Assert.AreEqual("virtue.peace", _healEvents[0].VirtueNameKey);
            Assert.AreEqual("Peace", _config.Strings.Get(_healEvents[0].VirtueNameKey));
            Assert.AreEqual(1, _healEvents[0].HealedTotal);
        }

        [Test]
        public void HealsAddUp()
        {
            _heal.Heal("strawberry");
            _heal.Heal("strawberry");
            _heal.Heal("strawberry");
            Assert.AreEqual(30, _state.Seeds);
            Assert.AreEqual(3, _state.HealedTotal);
            Assert.AreEqual(3, _healEvents[2].HealedTotal);
        }

        [Test]
        public void UnknownFruitHealsNobody()
        {
            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("unknown fruit"));
            Assert.AreEqual(0, _heal.Heal("no_such_fruit"));
            Assert.AreEqual(0, _state.Seeds);
            Assert.AreEqual(0, _state.HealedTotal);
            Assert.AreEqual(0, _healEvents.Count);
        }

        [Test]
        public void OnlyTheRequestedFruitHeals()
        {
            Assert.IsTrue(HealRules.Accepts("strawberry", "strawberry"));
            Assert.IsFalse(HealRules.Accepts("strawberry", "pineapple"));
            Assert.IsFalse(HealRules.Accepts(null, "strawberry"), "A visitor with no request accepts nothing.");
            Assert.IsFalse(HealRules.Accepts("", "strawberry"));
        }

        [Test]
        public void HudSeedsTextComesFromTheStringTable()
        {
            // Same key as HudView.SeedsKey (UI is not referenced by these tests).
            Assert.IsTrue(_config.Strings.Contains("ui.hud.seeds"));
            Assert.AreEqual("Seeds: 10", string.Format(_config.Strings.Get("ui.hud.seeds"), 10));
        }
    }
}
