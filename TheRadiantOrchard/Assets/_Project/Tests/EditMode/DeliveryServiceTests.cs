using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;
using UnityEngine;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Delivery (CLAUDE.md 5.5): path visitors before the bench, longest waiting first; fruit nobody asked for goes to
    /// the basket (3 per fruit, saved) and is handed out as soon as a matching visitor can be served; with the basket
    /// full and nobody asking, the fruit stays on the tree.
    /// </summary>
    public class DeliveryServiceTests
    {
        private const float Tick = 0.25f;

        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;
        private BalanceConfig _b;
        private GameState _state;
        private EventBus _events;
        private PlotManager _plots;
        private VisitorManager _visitors;
        private DeliveryService _delivery;
        private int _dirty;
        private List<DeliveryStarted> _started;
        private List<FruitBasketed> _basketed;
        private List<BasketChanged> _basketChanges;
        private List<HarvestHeld> _held;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _b = _config.Balance;
            _state = GameState.CreateNew(_b);
            _events = new EventBus();
            _dirty = 0;
            _started = new List<DeliveryStarted>();
            _basketed = new List<FruitBasketed>();
            _basketChanges = new List<BasketChanged>();
            _held = new List<HarvestHeld>();
            _events.Subscribe<DeliveryStarted>(_started.Add);
            _events.Subscribe<FruitBasketed>(_basketed.Add);
            _events.Subscribe<BasketChanged>(_basketChanges.Add);
            _events.Subscribe<HarvestHeld>(_held.Add);

            var time = new TimeService(new FakeClock());
            _plots = new PlotManager(_state, _config, time, _events, () => _dirty++);
            var heal = new HealService(_state, _config, _events, () => { });
            _visitors = new VisitorManager(_state, _config, time, _events, heal, () => _dirty++, new System.Random(7));
            _delivery = new DeliveryService(_state, _config, _events, _visitors, () => _dirty++);
            _plots.HarvestGate = _delivery.CanTake;
        }

        [TearDown]
        public void TearDown() => _delivery.Dispose();

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Tick)
            {
                _visitors.Tick(Tick);
                _delivery.Tick();
            }
        }

        private int ArriveAndWait()
        {
            int id = _visitors.SpawnNow();
            Run(_b.VisitorWalkInSeconds);
            return id;
        }

        private void Harvest(string fruitId) => _events.Publish(new FruitHarvested(0, fruitId));

        private int Basket(string fruitId) => _state.GetBasketCount(fruitId);

        // Plants strawberry on plot 1 so visitors come, and nothing else in the basket.
        private void PlantStrawberry() => Assert.AreEqual(PlantResult.Planted, _plots.TryPlant(0, "strawberry"));

        [Test]
        public void PathVisitorsComeBeforeTheBenchAndEachLongestWaitingFirst()
        {
            PlantStrawberry();
            int bench1 = ArriveAndWait();
            _visitors.RunOutPatienceNow();
            int bench2 = ArriveAndWait();
            _visitors.RunOutPatienceNow();
            Run(_b.VisitorWalkToBenchSeconds);
            int path1 = ArriveAndWait(), path2 = ArriveAndWait();

            for (int i = 0; i < 4; i++) Harvest("strawberry");

            Assert.AreEqual(new[] { path1, path2, bench1, bench2 }, _started.ConvertAll(e => e.VisitorId).ToArray());
            Assert.AreEqual(0, Basket("strawberry"));
            Assert.AreEqual(0, _started[0].FromPlot, "From the tree.");
        }

        [Test]
        public void FruitNobodyAskedForGoesIntoTheBasketAndIsSaved()
        {
            Harvest("lemon");
            Assert.AreEqual(0, _started.Count);
            Assert.AreEqual(1, _basketed.Count);
            Assert.AreEqual(1, _basketed[0].Count);
            Assert.AreEqual(1, Basket("lemon"));
            Assert.AreEqual(1, _basketChanges[0].Count);
            Assert.Greater(_dirty, 0);

            GameState loaded = JsonUtility.FromJson<GameState>(JsonUtility.ToJson(_state));
            Assert.AreEqual(1, loaded.GetBasketCount("lemon"), "The basket survives a restart.");
        }

        [Test]
        public void TheBasketHoldsThreeOfAFruitThenTheFruitStaysOnTheTree()
        {
            Assert.AreEqual(3, _b.BasketMaxPerFruit, "CLAUDE.md 5.5: max 3 per fruit.");
            _state.SetBasketCount("strawberry", 2);
            PlantStrawberry();
            _plots.RipenNow(0);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(0, GestureType.DoubleTap));
            Assert.AreEqual(3, Basket("strawberry"));

            _plots.RipenNow(0);
            Assert.IsFalse(_delivery.CanTake("strawberry"));
            Assert.AreEqual(HarvestJudgement.Held, _plots.HandleGesture(0, GestureType.DoubleTap));
            Assert.AreEqual(1, _held.Count);
            Assert.AreEqual(0, _held[0].Plot);
            Assert.AreEqual(TreeStage.Ripe, _plots.Get(0).Stage, "The fruit stays on the tree.");
            Assert.AreEqual(3, Basket("strawberry"));
            Assert.IsTrue(_delivery.CanTake("lemon"), "Other fruits still have room.");
        }

        [Test]
        public void AFullBasketStillLetsAHarvestGoToSomeoneWhoAskedForIt()
        {
            _state.SetBasketCount("strawberry", 3);
            PlantStrawberry();
            int visitor = _visitors.SpawnNow();
            // Only the visitors tick, so the basket has not served them yet when the tree is picked.
            for (float t = 0f; t < _b.VisitorWalkInSeconds; t += Tick) _visitors.Tick(Tick);

            Assert.IsTrue(_delivery.CanTake("strawberry"), "Someone is waiting for it.");
            _plots.RipenNow(0);
            Assert.AreEqual(HarvestJudgement.Harvest, _plots.HandleGesture(0, GestureType.DoubleTap));
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(0, _started[0].FromPlot);
            Assert.AreEqual(visitor, _started[0].VisitorId);
            Assert.AreEqual(3, Basket("strawberry"), "The basket is untouched.");
        }

        [Test]
        public void BasketFruitGoesToAMatchingVisitorOnceTheyReachTheirSpot()
        {
            _state.SetBasketCount("strawberry", 1);
            PlantStrawberry();
            int id = _visitors.SpawnNow();
            _delivery.Tick();
            Assert.AreEqual(0, _started.Count, "Not while they are still walking in.");

            Run(_b.VisitorWalkInSeconds);
            Assert.AreEqual(1, _started.Count);
            Assert.AreEqual(DeliveryService.FromBasket, _started[0].FromPlot);
            Assert.AreEqual(id, _started[0].VisitorId);
            Assert.AreEqual(0, Basket("strawberry"));

            Assert.AreEqual(_b.SeedsPerHeal, _delivery.Complete(id, "strawberry"));
            Assert.AreEqual(1, _state.HealedTotal);
        }

        [Test]
        public void BasketFruitGoesToVisitorsWhoArriveAfterTimeAwayAndToTheBench()
        {
            _state.SetBasketCount("strawberry", 1);
            PlantStrawberry();
            Assert.AreEqual(3, _visitors.ArriveAfterAway(TimeSpan.FromMinutes(10)));
            _delivery.Tick();
            Assert.AreEqual(_visitors.GetAt(0).Id, _started[0].VisitorId, "Offline arrivals can be served at once.");

            // Everyone else goes to the bench; a basket fruit then finds the longest seated.
            _visitors.RunOutPatienceNow();
            Run(_b.VisitorWalkToBenchSeconds);
            _state.SetBasketCount("strawberry", 1);
            _delivery.Tick();
            Assert.AreEqual(_visitors.GetBenchAt(0).Id, _started[1].VisitorId);
            Assert.AreEqual(_b.SeedsPerBenchHeal, _delivery.Complete(_started[1].VisitorId, "strawberry"), "Half seeds on the bench.");
        }

        [Test]
        public void EachVisitorGetsOnlyOneFruitFromTheBasket()
        {
            _state.SetBasketCount("strawberry", 2);
            PlantStrawberry();
            ArriveAndWait();
            Run(1f);
            Assert.AreEqual(1, _started.Count, "The second strawberry waits for a second visitor.");
            Assert.AreEqual(1, Basket("strawberry"));
        }

        [Test]
        public void AFruitThatCannotBeDeliveredGoesBackIntoTheBasket()
        {
            Assert.AreEqual(0, _delivery.Complete(999, "strawberry"), "Nobody by that id.");
            Assert.AreEqual(1, Basket("strawberry"), "Never lost.");
        }

        [Test]
        public void TargetingSkipsVisitorsWithATonicAlreadyOnTheWay()
        {
            PlantStrawberry();
            int first = ArriveAndWait(), second = ArriveAndWait();
            Assert.AreEqual(first, DeliveryTargeting.Pick(_visitors, "strawberry"));
            Assert.IsTrue(_visitors.TryReserve(first));
            Assert.IsFalse(_visitors.TryReserve(first), "Only once.");
            Assert.AreEqual(second, DeliveryTargeting.Pick(_visitors, "strawberry"));
            Assert.AreEqual(DeliveryTargeting.Nobody, DeliveryTargeting.Pick(_visitors, "lemon"));
        }
    }
}
