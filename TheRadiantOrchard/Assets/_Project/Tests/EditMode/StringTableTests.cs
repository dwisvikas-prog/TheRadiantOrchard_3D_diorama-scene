using System.Linq;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Checks the StringTable asset is complete and the lookup behaves.</summary>
    public class StringTableTests
    {
        private static readonly string[] VirtueIds =
            { "love", "joy", "peace", "patience", "meekness", "self_control", "kindness", "goodness", "faithfulness" };

        private static GameConfig Config()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            Assert.IsNotNull(config, "No GameConfig asset.");
            return config;
        }

        private static StringTable Strings()
        {
            var strings = Config().Strings;
            Assert.IsNotNull(strings, "GameConfig has no StringTable assigned.");
            return strings;
        }

        [Test]
        public void KeysAreUniqueAndTextIsNotEmpty()
        {
            var entries = Strings().Entries;
            CollectionAssert.AllItemsAreUnique(entries.Select(e => e.Key).ToList());
            foreach (var e in entries)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Key), "An entry has no key.");
                Assert.IsFalse(string.IsNullOrWhiteSpace(e.Text), $"Key '{e.Key}' has no text.");
            }
        }

        [Test]
        public void EveryFruitNameAndVirtueKeyExists()
        {
            var strings = Strings();
            foreach (var fruit in Config().Fruits)
            {
                Assert.IsTrue(strings.Contains(fruit.DisplayNameKey), $"Missing '{fruit.DisplayNameKey}'.");
                Assert.IsTrue(strings.Contains(fruit.VirtueNameKey), $"Missing '{fruit.VirtueNameKey}'.");
            }
        }

        [Test]
        public void SpecDefaultsAreInPlace()
        {
            var s = Strings();
            Assert.AreEqual("The Radiant Orchard", s.Get("game.name"));
            Assert.AreEqual("Wisdom Tree", s.Get("tree.wisdom.name"));
            Assert.AreEqual("Leave a prayer", s.Get("well.title"));
            Assert.AreEqual("Self-Control", s.Get("virtue.self_control"));
        }

        [Test]
        public void ClientContentSlotsExist()
        {
            var s = Strings();
            foreach (var virtue in VirtueIds)
            {
                for (int n = 1; n <= 3; n++)
                    Assert.IsTrue(s.Contains($"situation.{virtue}.{n}"), $"Missing situation.{virtue}.{n}.");
                Assert.IsTrue(s.Contains($"quiz.{virtue}.scenario"), $"Missing quiz.{virtue}.scenario.");
            }
        }

        [Test]
        public void PlantMenuKeysExistAndFormat()
        {
            var s = Strings();
            foreach (var key in new[] { "ui.plant.title", "ui.plant.seeds", "ui.plant.free", "ui.plant.price", "ui.plant.unlocks_at", "ui.close" })
                Assert.IsTrue(s.Contains(key), $"Missing '{key}'.");
            Assert.AreEqual("50 seeds", string.Format(s.Get("ui.plant.price"), 50));
            Assert.AreEqual("Unlocks at stage 2", string.Format(s.Get("ui.plant.unlocks_at"), 2));
        }

        [Test]
        public void MissingKeyIsVisibleAndWarns()
        {
            var table = ScriptableObject.CreateInstance<StringTable>();
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("missing string key 'no.such.key'"));
                Assert.AreEqual("#no.such.key", table.Get("no.such.key"));
                Assert.IsFalse(table.TryGet(null, out _));
            }
            finally
            {
                Object.DestroyImmediate(table);
            }
        }
    }
}
