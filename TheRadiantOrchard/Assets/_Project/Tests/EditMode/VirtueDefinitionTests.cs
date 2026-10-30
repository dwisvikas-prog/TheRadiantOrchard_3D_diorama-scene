using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// The nine virtues (CLAUDE.md 4, 5.4; ENGINEERING_GUIDE 5.4): each fruit links to the virtue the matrix gives it,
    /// and every virtue has its name and three situation lines in the StringTable.
    /// </summary>
    public class VirtueDefinitionTests
    {
        // CLAUDE.md section 4: fruit → virtue.
        private static readonly (string fruit, string virtue)[] Matrix =
        {
            ("strawberry", "love"), ("pineapple", "joy"), ("watermelon", "peace"),
            ("lemon", "patience"), ("grapes", "meekness"), ("apple", "self_control"),
            ("peach", "kindness"), ("banana", "goodness"), ("cherry", "faithfulness"),
        };

        private GameConfig _config;

        [SetUp]
        public void SetUp() => _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");

        [Test]
        public void GameConfigListsNineVirtuesInMatrixOrder()
        {
            Assert.AreEqual(9, _config.Virtues.Count);
            var ids = new HashSet<string>();
            for (int i = 0; i < Matrix.Length; i++)
            {
                Assert.IsNotNull(_config.Virtues[i], $"Virtues[{i}] is empty.");
                Assert.AreEqual(Matrix[i].virtue, _config.Virtues[i].Id);
                Assert.IsTrue(ids.Add(_config.Virtues[i].Id), "Ids are unique.");
            }
        }

        [Test]
        public void EveryFruitLinksToItsVirtue()
        {
            foreach ((string fruitId, string virtueId) in Matrix)
            {
                FruitDefinition fruit = _config.FindFruit(fruitId);
                Assert.IsNotNull(fruit.Virtue, $"{fruitId} has no virtue.");
                Assert.AreEqual(virtueId, fruit.Virtue.Id, fruitId);
                Assert.AreEqual($"virtue.{virtueId}", fruit.VirtueNameKey, fruitId);
                CollectionAssert.Contains(_config.Virtues, fruit.Virtue, $"{fruitId}'s virtue is on GameConfig.");
            }
        }

        [Test]
        public void EveryVirtueHasItsNameAndThreeLinesInTheStringTable()
        {
            foreach (VirtueDefinition virtue in _config.Virtues)
            {
                Assert.IsTrue(_config.Strings.Contains(virtue.NameKey), $"Missing '{virtue.NameKey}'.");
                Assert.AreEqual(3, virtue.SituationLineKeys.Count, $"CLAUDE.md 5.4: 3 lines per virtue ({virtue.Id}).");
                for (int n = 0; n < 3; n++)
                {
                    string key = virtue.SituationLineKeys[n];
                    Assert.AreEqual($"situation.{virtue.Id}.{n + 1}", key);
                    Assert.IsTrue(_config.Strings.Contains(key), $"Missing '{key}'.");
                }
            }
        }
    }
}
