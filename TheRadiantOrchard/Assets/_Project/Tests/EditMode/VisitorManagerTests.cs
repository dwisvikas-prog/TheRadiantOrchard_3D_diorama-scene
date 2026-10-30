using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Visitors on the path (CLAUDE.md 5.4): arrival rate and cap, requests only for planted fruit, spots, patience,
    /// longest-waiting targeting, and healing then leaving.
    /// </summary>
    public class VisitorManagerTests
    {
        private const float Tick = 0.25f;

        private GameConfig _config;
        private BalanceConfig _b;
        private GameState _state;
        private EventBus _events;
        private VisitorManager _visitors;
        private List<VisitorArrived> _arrived;
        private List<VisitorPatienceRanOut> _ranOut;
        private List<VisitorLeft> _left;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _b = _config.Balance;
            _state = GameState.CreateNew(_b);
            _events = new EventBus();
            _arrived = new List<VisitorArrived>();
            _ranOut = new List<VisitorPatienceRanOut>();
            _left = new List<VisitorLeft>();
            _events.Subscribe<VisitorArrived>(_arrived.Add);
            _events.Subscribe<VisitorPatienceRanOut>(_ranOut.Add);
            _events.Subscribe<VisitorLeft>(_left.Add);
            var heal = new HealService(_state, _config, _events, () => { });
            _visitors = new VisitorManager(_state, _config, _events, heal, new System.Random(12345));
        }

        private void Plant(int plot, string fruit) => _state.Plots[plot].FruitId = fruit;

        private void Run(float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Tick) _visitors.Tick(Tick);
        }

        // Spawn, then walk in, so the newest visitor is waiting at their spot.
        private int ArriveAndWait()
        {
            int id = _visitors.SpawnNow();
            Run(_b.VisitorWalkInSeconds);
            return id;
        }

        [Test]
        public void NoVisitorsComeUntilSomethingIsPlanted()
        {
            Run(_b.VisitorSpawnIntervalSeconds * 3);
            Assert.AreEqual(0, _visitors.Count);
            Assert.AreEqual(-1, _visitors.SpawnNow());
        }

        [Test]
        public void TheFirstVisitorComesRightAwayThenOneEverySpawnInterval()
        {
            Plant(0, "strawberry");
            _visitors.Tick(Tick);
            Assert.AreEqual(1, _visitors.Count, "First visitor on the first tick after planting.");

            Run(_b.VisitorSpawnIntervalSeconds - Tick * 2);
            Assert.AreEqual(1, _visitors.Count, "Not before the interval.");
            Run(Tick * 2);
            Assert.AreEqual(2, _visitors.Count, "The next after the spawn interval.");
        }

        [Test]
        public void NeverMoreThanTheMaximumOnThePath()
        {
            Plant(0, "strawberry");
            Run(_b.VisitorSpawnIntervalSeconds * 10);
            Assert.AreEqual(System.Math.Min(_b.MaxVisitorsOnPath, _config.Farm.WaitingSpotCells.Count), _visitors.Count);
            Assert.AreEqual(3, _visitors.Count, "CLAUDE.md 5.4: max 3 on the path.");
        }

        [Test]
        public void VisitorsOnlyAskForFruitThatIsPlanted()
        {
            Plant(0, "strawberry");
            Plant(1, "lemon");
            Plant(2, "lemon");
            var asked = new HashSet<string>();
            for (int round = 0; round < 40; round++)
            {
                int id = ArriveAndWait();
                Assert.GreaterOrEqual(id, 0);
                _visitors.TryGet(id, out VisitorInfo info);
                asked.Add(info.FruitId);
                Assert.Greater(_visitors.Deliver(id, info.FruitId), 0);
                Run(_b.VisitorWalkOutSeconds);
            }
            CollectionAssert.AreEquivalent(new[] { "strawberry", "lemon" }, asked, "Both planted fruits are asked for, nothing else.");
        }

        [Test]
        public void VisitorsTakeTheLowestFreeSpotAndFreeItWhenTheyLeave()
        {
            Plant(0, "strawberry");
            int a = ArriveAndWait(), b = ArriveAndWait(), c = ArriveAndWait();
            Assert.AreEqual(new[] { 0, 1, 2 }, new[] { _arrived[0].Spot, _arrived[1].Spot, _arrived[2].Spot });
            Assert.AreEqual(-1, _visitors.SpawnNow(), "All spots taken.");

            _visitors.Deliver(b, "strawberry");
            Run(_b.VisitorWalkOutSeconds);
            Assert.AreEqual(1, _left.Count);
            Assert.AreEqual(1, _left[0].Spot);

            int d = _visitors.SpawnNow();
            _visitors.TryGet(d, out VisitorInfo info);
            Assert.AreEqual(1, info.Spot, "The freed spot is reused.");
        }

        [Test]
        public void PatienceRunsDownAndRunsOutOnceWithoutAnyoneBeingSentAway()
        {
            Plant(0, "strawberry");
            int id = ArriveAndWait();
            _visitors.TryGet(id, out VisitorInfo info);
            Assert.AreEqual(VisitorPhase.Waiting, info.Phase);
            Assert.AreEqual(1f, info.Patience, 0.01f);

            Run(_b.PatienceSeconds / 2);
            _visitors.TryGet(id, out info);
            Assert.AreEqual(0.5f, info.Patience, 0.02f);
            Assert.AreEqual(0, _ranOut.Count);

            Run(_b.PatienceSeconds / 2 + 10f);
            Assert.AreEqual(1, _ranOut.FindAll(e => e.VisitorId == id).Count, "Ran out exactly once.");
            Assert.IsTrue(_visitors.TryGet(id, out info), "Nobody is turned away; the bench (W2·05) takes them.");
            Assert.IsTrue(info.PatienceRanOut);
        }

        [Test]
        public void TheLongestWaitingVisitorWhoAskedGetsTheTonicFirst()
        {
            Plant(0, "strawberry");
            int first = ArriveAndWait(), second = ArriveAndWait();
            Assert.AreEqual(first, _visitors.Reserve("strawberry"));
            Assert.AreEqual(second, _visitors.Reserve("strawberry"), "A second harvest goes to the next visitor.");
            Assert.AreEqual(-1, _visitors.Reserve("strawberry"), "Nobody else is waiting for one.");
            Assert.AreEqual(-1, _visitors.Reserve("lemon"), "Nobody asked for a lemon.");
        }

        [Test]
        public void VisitorsStillWalkingInAreNotTargeted()
        {
            Plant(0, "strawberry");
            _visitors.SpawnNow();
            Assert.AreEqual(-1, _visitors.Reserve("strawberry"));
        }

        [Test]
        public void AVisitorWithATonicOnTheWayDoesNotRunOutOfPatience()
        {
            Plant(0, "strawberry");
            int id = ArriveAndWait();
            Run(_b.PatienceSeconds - 1f);
            Assert.AreEqual(id, _visitors.Reserve("strawberry"));
            Run(10f);
            Assert.AreEqual(0, _ranOut.FindAll(e => e.VisitorId == id).Count);
        }

        [Test]
        public void ManualSpawnRestartsTheSpawnTimer()
        {
            Plant(0, "strawberry");
            _visitors.SpawnNow();
            Run(_b.VisitorSpawnIntervalSeconds - 1f);
            Assert.AreEqual(1, _visitors.Count, "No extra visitor right after a manual one.");
            Run(2f);
            Assert.AreEqual(2, _visitors.Count);
        }

        [Test]
        public void DeliveringHealsThenTheyWalkOff()
        {
            Plant(0, "strawberry");
            int id = ArriveAndWait();
            _visitors.Reserve("strawberry");

            Assert.AreEqual(_b.SeedsPerHeal, _visitors.Deliver(id, "strawberry"));
            Assert.AreEqual(_b.SeedsPerHeal, _state.Seeds);
            Assert.AreEqual(1, _state.HealedTotal);
            _visitors.TryGet(id, out VisitorInfo info);
            Assert.AreEqual(VisitorPhase.Leaving, info.Phase);

            Run(_b.VisitorWalkOutSeconds);
            Assert.IsFalse(_visitors.TryGet(id, out _), "Gone once they reach the exit.");
        }

        [Test]
        public void DeliveringToSomeoneGoneOrTheWrongFruitPaysNothing()
        {
            Plant(0, "strawberry");
            int id = ArriveAndWait();
            Assert.AreEqual(0, _visitors.Deliver(id, "lemon"), "They asked for a strawberry.");
            Assert.AreEqual(0, _visitors.Deliver(999, "strawberry"), "No such visitor.");
            Assert.AreEqual(0, _state.Seeds);
            Assert.AreEqual(0, _state.HealedTotal);
        }
    }
}
