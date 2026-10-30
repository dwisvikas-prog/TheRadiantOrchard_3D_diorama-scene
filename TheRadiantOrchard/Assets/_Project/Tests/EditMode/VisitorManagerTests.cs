using System;
using System.Collections.Generic;
using NUnit.Framework;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEditor;

namespace RadiantOrchard.Tests.EditMode
{
    /// <summary>
    /// Visitors (CLAUDE.md 5.4, 5.5): arrival rate and path cap, requests only for planted fruit, spots, patience,
    /// the bench (half seeds, saved, restored), the off-screen queue, path-before-bench targeting, healing then
    /// leaving, and that nobody is ever turned away.
    /// </summary>
    public class VisitorManagerTests
    {
        private const float Tick = 0.25f;

        private sealed class FakeClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
            public TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
        }

        private GameConfig _config;
        private BalanceConfig _b;
        private GameState _state;
        private FakeClock _clock;
        private TimeService _time;
        private EventBus _events;
        private HealService _heal;
        private VisitorManager _visitors;
        private int _dirty;
        private List<VisitorArrived> _arrived;
        private List<VisitorPatienceRanOut> _ranOut;
        private List<VisitorSatDown> _satDown;
        private List<VisitorLeft> _left;

        [SetUp]
        public void SetUp()
        {
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/GameConfig.asset");
            _b = _config.Balance;
            _state = GameState.CreateNew(_b);
            _clock = new FakeClock();
            _time = new TimeService(_clock);
            _events = new EventBus();
            _dirty = 0;
            _arrived = new List<VisitorArrived>();
            _ranOut = new List<VisitorPatienceRanOut>();
            _satDown = new List<VisitorSatDown>();
            _left = new List<VisitorLeft>();
            _events.Subscribe<VisitorArrived>(_arrived.Add);
            _events.Subscribe<VisitorPatienceRanOut>(_ranOut.Add);
            _events.Subscribe<VisitorSatDown>(_satDown.Add);
            _events.Subscribe<VisitorLeft>(_left.Add);
            _heal = new HealService(_state, _config, _events, () => { });
            _visitors = NewManager();
        }

        private VisitorManager NewManager() =>
            new VisitorManager(_state, _config, _time, _events, _heal, () => _dirty++, new Random(12345));

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

        private VisitorInfo Get(int id)
        {
            Assert.IsTrue(_visitors.TryGet(id, out VisitorInfo info), $"Visitor {id} is still here.");
            return info;
        }

        // Fills every bench seat with a strawberry visitor; returns their ids in seat order.
        private int[] FillBench()
        {
            var ids = new int[_visitors.SeatCount];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = ArriveAndWait();
                _visitors.RunOutPatienceNow();
            }
            Run(_b.VisitorWalkToBenchSeconds);
            return ids;
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
            int most = 0;
            for (int i = 0; i < 40; i++)
            {
                Run(_b.VisitorSpawnIntervalSeconds / 2);
                Assert.LessOrEqual(_visitors.PathCount, 3, "CLAUDE.md 5.4: max 3 on the path.");
                most = Math.Max(most, _visitors.PathCount);
            }
            Assert.AreEqual(Math.Min(_b.MaxVisitorsOnPath, _config.Farm.WaitingSpotCells.Count), most, "The path does fill up.");
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
                VisitorInfo info = Get(id);
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
            Assert.AreEqual(1, Get(d).Spot, "The freed spot is reused.");
        }

        [Test]
        public void PatienceRunsDownAndRunsOutOnceWithoutAnyoneBeingSentAway()
        {
            Plant(0, "strawberry");
            int id = ArriveAndWait();
            VisitorInfo info = Get(id);
            Assert.AreEqual(VisitorPhase.Waiting, info.Phase);
            Assert.AreEqual(1f, info.Patience, 0.01f);

            Run(_b.PatienceSeconds / 2);
            Assert.AreEqual(0.5f, Get(id).Patience, 0.02f);
            Assert.AreEqual(0, _ranOut.Count);

            Run(_b.PatienceSeconds / 2 + 10f);
            Assert.AreEqual(1, _ranOut.FindAll(e => e.VisitorId == id).Count, "Ran out exactly once.");
            info = Get(id);
            Assert.IsTrue(info.PatienceRanOut);
            Assert.AreEqual(VisitorPhase.Sitting, info.Phase, "Nobody is turned away; they sit on the bench.");
        }

        [Test]
        public void RunningOutOfPatienceFreesTheSpotAndTheySitOnTheBench()
        {
            Plant(0, "strawberry");
            int id = ArriveAndWait();
            Assert.AreEqual(1, _visitors.RunOutPatienceNow());

            VisitorInfo info = Get(id);
            Assert.AreEqual(VisitorPhase.WalkingToBench, info.Phase);
            Assert.AreEqual(-1, info.Spot, "They have left the path.");
            Assert.AreEqual(0, info.Seat);
            Assert.AreEqual(0, _visitors.PathCount);
            Assert.AreEqual(0, Get(_visitors.SpawnNow()).Spot, "Their spot is free for the next visitor at once.");
            Assert.AreEqual(0, _state.Bench.Count, "Saved only once seated.");

            Run(_b.VisitorWalkToBenchSeconds);
            Assert.AreEqual(VisitorPhase.Sitting, Get(id).Phase);
            Assert.AreEqual(1, _satDown.Count);
            Assert.AreEqual(id, _satDown[0].VisitorId);
            Assert.AreEqual(1, _state.Bench.Count);
            Assert.AreEqual("strawberry", _state.Bench[0].FruitId);
            Assert.AreEqual(TimeService.ToUnixMs(_clock.UtcNow), _state.Bench[0].ArrivedUtc);
            Assert.Greater(_dirty, 0, "The bench change is saved.");

            Run(_b.PatienceSeconds * 3);
            Assert.AreEqual(VisitorPhase.Sitting, Get(id).Phase, "No patience on the bench; they wait as long as it takes.");
        }

        [Test]
        public void ABenchHealPaysHalfFreesTheSeatAndLeavesTheSave()
        {
            Plant(0, "strawberry");
            int id = FillBench()[0];
            Assert.AreEqual(id, _visitors.Reserve("strawberry"));

            Assert.AreEqual(_b.SeedsPerBenchHeal, _visitors.Deliver(id, "strawberry"));
            Assert.AreEqual(5, _state.Seeds, "CLAUDE.md 5.6: 5 seeds from the bench.");
            Assert.AreEqual(1, _state.HealedTotal, "Still counts towards the progress bar.");
            Assert.AreEqual(_visitors.SeatCount - 1, _state.Bench.Count);
            Assert.AreEqual(_visitors.SeatCount - 1, _visitors.BenchCount, "The seat is free.");
            VisitorInfo info = Get(id);
            Assert.AreEqual(VisitorPhase.Leaving, info.Phase);
            Assert.AreEqual(-1, info.Seat);

            Run(_b.VisitorWalkOutSeconds);
            Assert.IsFalse(_visitors.TryGet(id, out _));
            Assert.AreEqual(-1, _left.Find(e => e.VisitorId == id).Spot, "Left from the bench, not a path spot.");
        }

        [Test]
        public void PathVisitorsAreServedBeforeTheBenchAndEachLongestWaitingFirst()
        {
            Plant(0, "strawberry");
            int[] bench = FillBench();
            int path1 = ArriveAndWait(), path2 = ArriveAndWait();

            Assert.AreEqual(path1, _visitors.Reserve("strawberry"));
            Assert.AreEqual(path2, _visitors.Reserve("strawberry"));
            for (int i = 0; i < bench.Length; i++)
                Assert.AreEqual(bench[i], _visitors.Reserve("strawberry"), $"Then the bench, longest seated first ({i}).");
            Assert.AreEqual(-1, _visitors.Reserve("strawberry"));
        }

        [Test]
        public void WithTheBenchFullTheyWaitOffscreenAndTakeTheFirstSeatThatFrees()
        {
            Plant(0, "strawberry");
            int[] bench = FillBench();
            int d = ArriveAndWait(), e = ArriveAndWait();
            Assert.AreEqual(2, _visitors.RunOutPatienceNow());
            Assert.AreEqual(VisitorPhase.WalkingAway, Get(d).Phase, "Bench full: back into the jungle.");
            Assert.AreEqual(-1, Get(d).Seat);

            Run(_b.VisitorWalkInSeconds);
            Assert.AreEqual(VisitorPhase.Offscreen, Get(d).Phase);
            Assert.AreEqual(VisitorPhase.Offscreen, Get(e).Phase);
            Assert.AreEqual(2, _visitors.OffscreenCount);
            Assert.AreEqual(-1, _visitors.Reserve("lemon"));
            Assert.AreEqual(bench[0], _visitors.Reserve("strawberry"), "Off-screen visitors are never targeted.");

            _visitors.Deliver(bench[1], "strawberry");
            VisitorInfo next = Get(d);
            Assert.AreEqual(VisitorPhase.WalkingToBench, next.Phase, "The first one off-screen comes for the seat.");
            Assert.AreEqual(1, next.Seat);
            Assert.AreEqual(VisitorPhase.Offscreen, Get(e).Phase);

            Run(_b.VisitorWalkToBenchSeconds);
            Assert.AreEqual(VisitorPhase.Sitting, Get(d).Phase);
        }

        [Test]
        public void NobodyIsEverTurnedAway()
        {
            Plant(0, "strawberry");
            Run(20 * 60f);
            Assert.AreEqual(0, _left.Count, "Nobody leaves unhealed.");
            Assert.AreEqual(_arrived.Count, _visitors.Count, "Everyone who came is still here.");
            Assert.AreEqual(_visitors.Count, _visitors.PathCount + _visitors.BenchCount + _visitors.OffscreenCount);
            Assert.AreEqual(_visitors.SeatCount, _visitors.BenchCount);
            Assert.Greater(_visitors.OffscreenCount, 0);
        }

        [Test]
        public void SeatedVisitorsComeBackFromTheSaveInTheOrderTheySat()
        {
            _state.Bench.Add(new BenchVisitorState("lemon", 300));
            _state.Bench.Add(new BenchVisitorState("strawberry", 100));
            _state.Bench.Add(new BenchVisitorState("no_such_fruit", 50));
            _state.Bench.Add(new BenchVisitorState("pineapple", 200));
            _state.Bench.Add(new BenchVisitorState("apple", 400));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("no_such_fruit"));
            _visitors = NewManager();

            Assert.AreEqual(4, _state.Bench.Count, "The unknown fruit is dropped from the save.");
            Assert.AreEqual(4, _visitors.Count);
            string[] fruits = { "strawberry", "pineapple", "lemon", "apple" };
            for (int i = 0; i < 4; i++) Assert.AreEqual(fruits[i], _visitors.GetAt(i).FruitId);
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(VisitorPhase.Sitting, _visitors.GetAt(i).Phase);
                Assert.AreEqual(i, _visitors.GetAt(i).Seat);
            }
            Assert.AreEqual(VisitorPhase.Offscreen, _visitors.GetAt(3).Phase, "More saved than seats: the rest wait off-screen.");
            Assert.AreEqual(0, _satDown.Count, "Restored, not newly seated.");

            int strawberry = _visitors.GetAt(0).Id;
            Assert.AreEqual(strawberry, _visitors.Reserve("strawberry"));
            Assert.AreEqual(_b.SeedsPerBenchHeal, _visitors.Deliver(strawberry, "strawberry"));
            Assert.AreEqual(3, _state.Bench.Count);
            Assert.AreEqual(VisitorPhase.WalkingToBench, _visitors.GetAt(3).Phase, "The apple visitor takes the freed seat.");

            Run(_b.VisitorWalkToBenchSeconds);
            Assert.AreEqual(3, _state.Bench.Count, "Already saved, not saved twice.");
        }

        [Test]
        public void RunningOutPatienceNowSkipsVisitorsWithATonicComing()
        {
            Plant(0, "strawberry");
            int a = ArriveAndWait(), b = ArriveAndWait();
            _visitors.Reserve("strawberry");
            Assert.AreEqual(1, _visitors.RunOutPatienceNow());
            Assert.AreEqual(VisitorPhase.Waiting, Get(a).Phase);
            Assert.AreEqual(VisitorPhase.WalkingToBench, Get(b).Phase);
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
            Assert.AreEqual(VisitorPhase.Leaving, Get(id).Phase);

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

        [Test]
        public void AfterTimeAwayUpToThreeVisitorsAreAlreadyWaiting()
        {
            Plant(0, "strawberry");
            Assert.AreEqual(3, _visitors.ArriveAfterAway(TimeSpan.FromMinutes(10)));
            Assert.AreEqual(3, _visitors.PathCount);
            for (int i = 0; i < 3; i++)
            {
                VisitorInfo info = _visitors.GetAt(i);
                Assert.AreEqual(VisitorPhase.Waiting, info.Phase, "Already at their spot, no walk in.");
                Assert.AreEqual(i, info.Spot);
                Assert.AreEqual(1f, info.Patience, 1e-4f, "Full patience.");
            }
            Assert.AreEqual(_visitors.GetAt(0).Id, _visitors.Reserve("strawberry"), "They can be served straight away.");
        }

        [Test]
        public void OneVisitorPerSpawnIntervalAwayAndNoneForAShortBreak()
        {
            Plant(0, "strawberry");
            Assert.AreEqual(0, _visitors.ArriveAfterAway(TimeSpan.FromSeconds(_b.VisitorSpawnIntervalSeconds - 1f)));
            Assert.AreEqual(0, _visitors.ArriveAfterAway(TimeSpan.Zero));
            Assert.AreEqual(2, _visitors.ArriveAfterAway(TimeSpan.FromSeconds(_b.VisitorSpawnIntervalSeconds * 2.5f)));
            Assert.AreEqual(1, _visitors.ArriveAfterAway(TimeSpan.FromDays(3)), "Only the free spot is filled.");
            Assert.AreEqual(0, _visitors.ArriveAfterAway(TimeSpan.FromDays(3)), "The path is full.");
            Assert.AreEqual(_b.MaxOfflineArrivals, 3, "CLAUDE.md 5.4: up to 3.");
        }

        [Test]
        public void NobodyArrivesAfterTimeAwayWithNothingPlanted()
        {
            Assert.AreEqual(0, _visitors.ArriveAfterAway(TimeSpan.FromHours(5)));
            Assert.AreEqual(0, _visitors.Count);
        }

        [Test]
        public void OfflineArrivalsRestartTheSpawnTimer()
        {
            Plant(0, "strawberry");
            Assert.AreEqual(2, _visitors.ArriveAfterAway(TimeSpan.FromSeconds(_b.VisitorSpawnIntervalSeconds * 2)));
            // A new game would bring its first visitor on the first tick; after offline arrivals the next waits.
            Run(_b.VisitorSpawnIntervalSeconds - 1f);
            Assert.AreEqual(2, _arrived.Count, "No regular visitor right after the offline ones.");
            Run(2f);
            Assert.AreEqual(3, _arrived.Count, "The third spot fills after a full interval.");
        }

        [Test]
        public void EachVisitorSaysALineOfTheirFruitsVirtue()
        {
            foreach (FruitDefinition fruit in _config.Fruits)
            {
                SetUp();
                Plant(0, fruit.Id);
                var said = new HashSet<string>();
                for (int round = 0; round < 30; round++)
                {
                    int id = _visitors.SpawnNow();
                    VisitorInfo info = Get(id);
                    StringAssert.StartsWith($"situation.{fruit.Virtue.Id}.", info.LineKey, fruit.Id);
                    CollectionAssert.Contains(fruit.Virtue.SituationLineKeys, info.LineKey);
                    said.Add(info.LineKey);
                    Run(_b.VisitorWalkInSeconds);
                    _visitors.Deliver(id, fruit.Id);
                    Run(_b.VisitorWalkOutSeconds);
                }
                Assert.AreEqual(3, said.Count, $"All three {fruit.Virtue.Id} lines are used.");
            }
        }

        [Test]
        public void VisitorsBackFromTheBenchSaveSayNothing()
        {
            _state.Bench.Add(new BenchVisitorState("strawberry", 100));
            _visitors = NewManager();
            Assert.IsNull(_visitors.GetAt(0).LineKey, "They spoke when they first arrived.");
        }
    }
}
