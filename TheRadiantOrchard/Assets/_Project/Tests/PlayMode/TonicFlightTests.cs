using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using RadiantOrchard.App;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RadiantOrchard.Tests.PlayMode
{
    /// <summary>
    /// Tonic flight in the real Game scene (CLAUDE.md 5.5, ENGINEERING_GUIDE 7): a harvest launches a pooled tonic
    /// that lands on its target and goes back to the pool, and a flight allocates nothing per frame.
    /// Saves go to a temporary folder, never the real save.
    /// </summary>
    public class TonicFlightTests
    {
        private const float FlightAndPop = 0.8f + 0.15f;
        private string _saveDir;
        private AppServices _services;
        private TonicFlights _tonics;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _saveDir = Path.Combine(Path.GetTempPath(), "RadiantOrchardTonicTest", Guid.NewGuid().ToString("N"));
            yield return TestGame.Boot(_saveDir);

            _services = AppServices.Current;
            // Test code may search the scene; the no-Find rule is for runtime code.
            _tonics = Object.FindFirstObjectByType<TonicFlights>();
            Assert.IsNotNull(_services, "Boot did not start.");
            Assert.IsNotNull(_tonics, "No TonicFlights in Game (Radiant Orchard > Setup > Build Tonic).");
        }

        [TearDown]
        public void TearDown()
        {
            TestGame.Shutdown(_saveDir);
        }

        [UnityTest]
        public IEnumerator HarvestSendsATonicThatLandsOnTheTargetAndReturnsToThePool()
        {
            int arrivals = 0;
            string arrivedFruit = null;
            Vector2 landed = default;
            _tonics.Arrived += (fruit, token, point) =>
            {
                arrivals++;
                arrivedFruit = fruit;
                landed = point;
            };
            int pooled = _tonics.PooledCount;

            _services.Events.Publish(new FruitHarvested(0, "strawberry"));
            Assert.AreEqual(1, _tonics.ActiveCount, "A harvest launches one tonic.");

            yield return new WaitForSeconds(FlightAndPop + 0.3f);

            Assert.AreEqual(1, arrivals);
            Assert.AreEqual("strawberry", arrivedFruit);
            // Nobody is waiting yet, so the tonic lands over the first waiting spot.
            var farm = Object.FindFirstObjectByType<FarmView>();
            Assert.Less(Vector2.Distance(landed, farm.WaitingSpots[0].position), 2f, "The tonic lands at its target.");
            Assert.AreEqual(0, _tonics.ActiveCount, "Back in the pool after the pop.");
            Assert.AreEqual(pooled, _tonics.PooledCount, "No new tonic was created.");
        }

        [UnityTest]
        public IEnumerator ThreeHarvestsFlyTogetherAndThePoolIsReused()
        {
            int pooled = _tonics.PooledCount;
            for (int plot = 0; plot < 3; plot++) _services.Events.Publish(new FruitHarvested(plot, "strawberry"));
            Assert.AreEqual(3, _tonics.ActiveCount);

            yield return new WaitForSeconds(FlightAndPop + 0.3f);
            Assert.AreEqual(0, _tonics.ActiveCount);

            for (int plot = 0; plot < 3; plot++) _services.Events.Publish(new FruitHarvested(plot, "strawberry"));
            Assert.AreEqual(3, _tonics.ActiveCount);
            Assert.AreEqual(pooled, _tonics.PooledCount, "Pre-warmed tonics are reused, not re-created.");
        }

        [UnityTest]
        public IEnumerator AFlightAllocatesNothingPerFrame()
        {
            _tonics.Launch(Vector2.zero, new Vector2(6f, 2f), Color.red, "strawberry", -1);
            const float dt = 1f / 60f;
            _tonics.Step(dt);

            long before = GC.GetAllocatedBytesForCurrentThread();
            // 70 frames covers the whole flight, the arrival, the pop and the return to the pool.
            for (int i = 0; i < 70; i++) _tonics.Step(dt);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.AreEqual(0, _tonics.ActiveCount, "The flight finished within the measured frames.");
            Assert.AreEqual(0, allocated, "Bytes allocated while tonics were in flight.");
            yield return null;
        }
    }
}
