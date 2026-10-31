using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.Dev;
using RadiantOrchard.Gameplay;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// Mini-events in the running game (CLAUDE.md 5.11): each dev button shows its banner and its effect. Saves go to a
    /// temporary folder.
    /// </summary>
    public class MiniEventPlayTests
    {
        private string _saveDir;
        private DevGame _game;
        private ZoneBannerView _banner;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardEventTest", Guid.NewGuid().ToString("N"));
            yield return TestGame.Boot(_saveDir);
            _game = DevGame.Current;
            Assert.IsNotNull(_game?.MiniEvents, "GameInstaller did not finish wiring.");
            // Test code may search the scene; the no-Find rule is for runtime code.
            _banner = Object.FindFirstObjectByType<ZoneBannerView>();
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        [UnityTest]
        public IEnumerator EachEventShowsItsBannerAndDoesItsJob()
        {
            DevActions.MiniEvent(_game, "golden_hour");
            yield return null;
            Assert.IsTrue(_banner.IsShowing);
            Assert.AreEqual("Golden Hour!", _banner.Title);
            Assert.AreEqual("Double seeds for 1:30", _banner.Body);
            Assert.IsTrue(_game.Boosts.IsActive(BoostService.GoldenHarvest));
            var golden = Object.FindFirstObjectByType<GoldenHarvestView>();
            Assert.IsTrue(golden.IsShowing, "The gold glow and timer come with it.");

            Assert.AreEqual(PlantResult.Planted, _game.Plots.TryPlant(0, "strawberry"));
            yield return null;
            DevActions.MiniEvent(_game, "blessing_rain");
            yield return null;
            Assert.AreEqual("Blessing Rain!", _banner.Title);
            Assert.IsTrue(_game.Plots.Get(0).IsRipe);

            // A regular visitor may already have come for the new tree; the rush fills whatever spots are left.
            DevActions.MiniEvent(_game, "visitor_rush");
            yield return null;
            Assert.AreEqual("Visitor Rush!", _banner.Title);
            Assert.AreEqual(0, _game.Visitors.FreeSpots);
        }
    }
}
