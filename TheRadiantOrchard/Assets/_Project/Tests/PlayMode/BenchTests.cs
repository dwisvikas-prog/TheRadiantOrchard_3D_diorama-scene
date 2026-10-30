using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// The bench across a restart (CLAUDE.md 5.4): a visitor saved on the bench is sitting there at launch, a tonic
    /// reaches them, they pay half seeds and leave the save. Saves go to a temporary folder, never the real save.
    /// </summary>
    public class BenchTests
    {
        private const float TonicFlightAndPop = 0.8f + 0.15f + 0.3f;
        private string _saveDir;
        private AppServices _services;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardBenchTest", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saveDir);
            // A save from an earlier session with one visitor on the bench (plots are added on load).
            var saved = new GameState();
            saved.Bench.Add(new BenchVisitorState("strawberry", 1_700_000_000_000));
            File.WriteAllText(Path.Combine(_saveDir, LocalFileStore.MainFileName), JsonUtility.ToJson(saved));

            yield return TestGame.Boot(_saveDir);

            _services = AppServices.Current;
            Assert.IsNotNull(_services, "Boot did not start.");
            Assert.IsNotNull(DevGame.Current?.Visitors, "GameInstaller did not finish wiring (check the console for missing references).");
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        [UnityTest]
        public IEnumerator ASavedBenchVisitorIsSeatedAtLaunchAndPaysHalfWhenHealed()
        {
            VisitorManager visitors = DevGame.Current.Visitors;
            Assert.AreEqual(1, visitors.Count);
            VisitorInfo info = visitors.GetAt(0);
            Assert.AreEqual(VisitorPhase.Sitting, info.Phase);
            Assert.AreEqual("strawberry", info.FruitId);

            // Test code may search the scene; the no-Find rule is for runtime code.
            var presenter = Object.FindFirstObjectByType<VisitorsPresenter>();
            VisitorView view = presenter.ViewFor(info.Id);
            Assert.IsNotNull(view, "The seated visitor has a figure.");
            Assert.IsTrue(view.gameObject.activeInHierarchy);
            Assert.Less(Vector2.Distance(view.transform.position, presenter.SeatPosition(info.Seat)), 0.01f, "On their seat.");

            // As a harvest would: the tonic flies to them and lands.
            _services.Events.Publish(new FruitHarvested(0, "strawberry"));
            yield return new WaitForSeconds(TonicFlightAndPop);

            Assert.AreEqual(_services.Config.Balance.SeedsPerBenchHeal, _services.State.Seeds, "+5 seeds from the bench.");
            Assert.AreEqual(1, _services.State.HealedTotal);
            Assert.AreEqual(0, _services.State.Bench.Count, "No longer saved on the bench.");
            Assert.AreEqual(VisitorPhase.Leaving, visitors.GetAt(0).Phase);
        }
    }
}
