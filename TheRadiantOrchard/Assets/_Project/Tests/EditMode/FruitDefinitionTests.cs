using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RadiantOrchard.Core;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>Keeps the nine fruit assets in step with the fruit table in CLAUDE.md section 4.</summary>
    public class FruitDefinitionTests
    {
        private const string FruitFolder = "Assets/_Project/Data/Fruits";

        private static List<FruitDefinition> LoadAll()
        {
            return AssetDatabase.FindAssets("t:" + nameof(FruitDefinition), new[] { FruitFolder })
                .Select(guid => AssetDatabase.LoadAssetAtPath<FruitDefinition>(AssetDatabase.GUIDToAssetPath(guid)))
                .ToList();
        }

        [Test]
        public void ThereAreExactlyNineFruits()
        {
            Assert.AreEqual(9, LoadAll().Count);
        }

        [Test]
        public void IdsAreUniqueAndLowercase()
        {
            var ids = LoadAll().Select(f => f.Id).ToList();
            CollectionAssert.AllItemsAreUnique(ids);
            foreach (var id in ids)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(id), "A fruit has no id.");
                Assert.AreEqual(id.Trim().ToLowerInvariant(), id, $"Id '{id}' must be lowercase without spaces.");
            }
        }

        [TestCase("strawberry", "virtue.love", GestureType.DoubleTap, 0, 5 * 60, 0)]
        [TestCase("pineapple", "virtue.joy", GestureType.DoubleTap, 0, 15 * 60, 50)]
        [TestCase("watermelon", "virtue.peace", GestureType.DoubleTap, 0, 30 * 60, 120)]
        [TestCase("lemon", "virtue.patience", GestureType.LongPress, 2, 60 * 60, 250)]
        [TestCase("grapes", "virtue.meekness", GestureType.LongPress, 2, 2 * 60 * 60, 400)]
        [TestCase("apple", "virtue.self_control", GestureType.LongPress, 3, 3 * 60 * 60, 600)]
        [TestCase("peach", "virtue.kindness", GestureType.Swipe, 3, 4 * 60 * 60, 900)]
        [TestCase("banana", "virtue.goodness", GestureType.Swipe, 4, 6 * 60 * 60, 1300)]
        [TestCase("cherry", "virtue.faithfulness", GestureType.Swipe, 4, 8 * 60 * 60, 1800)]
        public void FruitMatchesSpecTable(string id, string virtueKey, GestureType gesture, int unlockStage, int growSeconds, int cost)
        {
            var fruit = LoadAll().SingleOrDefault(f => f.Id == id);
            Assert.IsNotNull(fruit, $"No FruitDefinition with id '{id}' in {FruitFolder}.");
            Assert.AreEqual("fruit." + id, fruit.DisplayNameKey);
            Assert.AreEqual(virtueKey, fruit.VirtueNameKey);
            Assert.AreEqual(gesture, fruit.Gesture);
            Assert.AreEqual(unlockStage, fruit.UnlockStage);
            Assert.AreEqual(growSeconds, fruit.GrowSeconds);
            Assert.AreEqual(cost, fruit.PlantCost);
        }
    }
}
