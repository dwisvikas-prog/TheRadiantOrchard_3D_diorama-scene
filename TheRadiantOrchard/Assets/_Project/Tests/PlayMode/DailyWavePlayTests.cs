using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The daily visitor wave in the running game (CLAUDE.md 5.10): a save from yesterday with a tree planted launches
    /// into the wave (banner, three visitors walking in); healing all three pays the bonus and says so. Saves go to a
    /// temporary folder.
    /// </summary>
    public class DailyWavePlayTests
    {
        private string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardWaveTest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            var saved = new GameState
            {
                LastDailyWaveDate = DateTime.Now.AddDays(-1).ToString(TimeService.LocalDateFormat, CultureInfo.InvariantCulture),
            };
            File.WriteAllText(Path.Combine(_saveDir, LocalFileStore.MainFileName), JsonUtility.ToJson(saved));
            yield return TestGame.Boot(_saveDir);
            Assert.IsNotNull(DevGame.Current?.DailyWave, "GameInstaller did not finish wiring.");
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        [UnityTest]
        public IEnumerator YesterdaysSaveLaunchesIntoTheWaveAndHealingAllPaysTheBonus()
        {
            DevGame game = DevGame.Current;
            // Test code may search the scene; the no-Find rule is for runtime code.
            var banner = Object.FindFirstObjectByType<ZoneBannerView>();

            // Nothing planted yet: the wave waits (and the day is not used up).
            Assert.IsTrue(game.DailyWave.IsPending);
            Assert.AreEqual(PlantResult.Planted, game.Plots.TryPlant(0, "strawberry"));
            yield return new WaitForSeconds(0.6f);

            Assert.IsFalse(game.DailyWave.IsPending);
            Assert.IsTrue(game.DailyWave.IsActive);
            Assert.IsTrue(banner.IsShowing);
            Assert.AreEqual(game.Config.Strings.Get(DailyWaveAnnouncer.TitleKey), banner.Title);
            Assert.AreEqual("Heal all 3 for 100 bonus seeds", banner.Body);

            var wave = new List<VisitorInfo>();
            for (int i = 0; i < game.Visitors.Count; i++) wave.Add(game.Visitors.GetAt(i));
            Assert.AreEqual(3, wave.Count);
            Assert.AreEqual(game.Config.Balance.DailyWavePatienceSeconds, wave[0].PatienceSeconds);

            yield return new WaitForSeconds(game.Config.Balance.VisitorWalkInSeconds + 0.5f);
            int seeds = game.State.Seeds;
            int paid = 0;
            foreach (VisitorInfo v in wave)
            {
                int heal = game.Visitors.Deliver(v.Id, v.FruitId);
                Assert.Greater(heal, 0);
                paid += heal;
            }

            Assert.IsFalse(game.DailyWave.IsActive);
            Assert.AreEqual(seeds + paid + 100, game.State.Seeds, "Three heals (with their combo) and the 100 bonus.");
            Assert.AreEqual(game.Config.Strings.Get(DailyWaveAnnouncer.DoneTitleKey), banner.Title);
        }
    }
}
