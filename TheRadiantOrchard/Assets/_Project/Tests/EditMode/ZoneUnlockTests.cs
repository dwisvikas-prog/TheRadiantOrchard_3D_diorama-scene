using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Input;
using RadiantOrchard.UI;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Zone unlock sequence pieces (CLAUDE.md 5.7): what each stage opens, the banner text built from it, and the
    /// camera framing of a zone.
    /// </summary>
    public class ZoneUnlockTests
    {
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
        }

        private static List<string> Ids(StageUnlocks unlocks) =>
            new List<FruitDefinition>(unlocks.Fruits).ConvertAll(f => f.Id);

        [Test]
        public void EachStageOpensTheSpecsFruitsAndPlots()
        {
            StageUnlocks one = StageUnlocks.For(_config, 1);
            CollectionAssert.IsEmpty(Ids(one));
            Assert.AreEqual(0, one.NewPlots);
            Assert.IsFalse(one.IsFinal);

            StageUnlocks two = StageUnlocks.For(_config, 2);
            CollectionAssert.AreEqual(new[] { "lemon", "grapes" }, Ids(two));
            Assert.AreEqual(2, two.NewPlots);

            StageUnlocks three = StageUnlocks.For(_config, 3);
            CollectionAssert.AreEqual(new[] { "apple", "peach" }, Ids(three));
            Assert.AreEqual(2, three.NewPlots);

            StageUnlocks four = StageUnlocks.For(_config, 4);
            CollectionAssert.AreEqual(new[] { "banana", "cherry" }, Ids(four));
            Assert.AreEqual(2, four.NewPlots);

            StageUnlocks five = StageUnlocks.For(_config, 5);
            CollectionAssert.IsEmpty(Ids(five));
            Assert.AreEqual(0, five.NewPlots);
            Assert.IsTrue(five.IsFinal);
        }

        [Test]
        public void TheStartIsNotAStageUnlock()
        {
            StageUnlocks start = StageUnlocks.For(_config, 0);
            CollectionAssert.IsEmpty(Ids(start), "Starting fruits are not announced.");
            Assert.AreEqual(0, start.NewPlots);
        }

        [Test]
        public void TheBannerNamesTheNewFruitsAndPlots()
        {
            StringTable strings = _config.Strings;
            Assert.AreEqual("Stage 2!", string.Format(strings.Get(ZoneBannerView.TitleKey), 2));
            Assert.AreEqual("New fruit: Lemon, Grapes\n2 new plots", ZoneBannerView.BodyFor(StageUnlocks.For(_config, 2), strings));
            Assert.AreEqual("New fruit: Banana, Cherry\n2 new plots", ZoneBannerView.BodyFor(StageUnlocks.For(_config, 4), strings));
            Assert.AreEqual(strings.Get(ZoneBannerView.ZoneKey), ZoneBannerView.BodyFor(StageUnlocks.For(_config, 1), strings));
            Assert.AreEqual(strings.Get(ZoneBannerView.CompleteKey), ZoneBannerView.BodyFor(StageUnlocks.For(_config, 5), strings));
            foreach (string key in new[] { ZoneBannerView.TitleKey, ZoneBannerView.FruitsKey, ZoneBannerView.PlotsKey, ZoneBannerView.ZoneKey, ZoneBannerView.CompleteKey })
                Assert.IsTrue(strings.Contains(key), key);
        }

        [Test]
        public void FitSizeFramesTheAreaWithAMargin()
        {
            const float aspect = 16f / 9f;
            // Wide area: the width decides (20 / (2 × 16/9) = 5.625).
            Assert.AreEqual(5.625f, CameraMath.FitSize(new Rect(0f, 0f, 20f, 4f), aspect, 0f), 1e-4f);
            // Tall area: the height decides.
            Assert.AreEqual(6f, CameraMath.FitSize(new Rect(0f, 0f, 4f, 12f), aspect, 0f), 1e-4f);
            Assert.AreEqual(6.9f, CameraMath.FitSize(new Rect(0f, 0f, 4f, 12f), aspect, 0.15f), 1e-4f);
            Assert.AreEqual(6f, CameraMath.FitSize(new Rect(0f, 0f, 4f, 12f), aspect, -1f), 1e-4f, "No negative margin.");
        }
    }
}
