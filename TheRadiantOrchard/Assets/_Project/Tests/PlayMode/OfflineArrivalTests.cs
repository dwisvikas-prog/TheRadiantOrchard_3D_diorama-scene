using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// Time away (CLAUDE.md 5.4): launching a save last written 10 minutes ago, with a strawberry planted, finds three
    /// visitors already waiting at their spots, each showing a Love situation line. Saves go to a temporary folder.
    /// </summary>
    public class OfflineArrivalTests
    {
        private string _saveDir;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardOfflineTest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            long tenMinutesAgo = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeMilliseconds();
            // Plot 1 open with a strawberry; load adds the other plots.
            string json = "{\"_version\":1,\"_lastSaveUtc\":" + tenMinutesAgo
                + ",\"_plots\":[{\"_unlocked\":true,\"_fruitId\":\"strawberry\",\"_plantedUtc\":" + tenMinutesAgo + "}]}";
            File.WriteAllText(Path.Combine(_saveDir, LocalFileStore.MainFileName), json);

            yield return TestGame.Boot(_saveDir);
            Assert.IsNotNull(DevGame.Current?.Visitors, "Boot did not start.");
        }

        [TearDown]
        public void TearDown() => TestGame.Shutdown(_saveDir);

        [UnityTest]
        public IEnumerator ThreeVisitorsAreWaitingAtLaunchEachWithALoveLine()
        {
            VisitorManager visitors = DevGame.Current.Visitors;
            Assert.GreaterOrEqual(AppServices.Current.LaunchTimeAway, TimeSpan.FromMinutes(10));
            Assert.AreEqual(3, visitors.PathCount);

            // Test code may search the scene; the no-Find rule is for runtime code.
            var presenter = Object.FindFirstObjectByType<VisitorsPresenter>();
            StringTable strings = AppServices.Current.Config.Strings;
            for (int i = 0; i < visitors.Count; i++)
            {
                VisitorInfo info = visitors.GetAt(i);
                Assert.AreEqual(VisitorPhase.Waiting, info.Phase, "Already at their spot.");
                StringAssert.StartsWith("situation.love.", info.LineKey, "Strawberry stands for Love.");

                VisitorView view = presenter.ViewFor(info.Id);
                Assert.IsNotNull(view);
                Assert.IsTrue(view.LineShown, "The line shows on arrival.");
                Assert.AreEqual(strings.Get(info.LineKey), view.LineText);
            }
            yield return null;
        }
    }
}
