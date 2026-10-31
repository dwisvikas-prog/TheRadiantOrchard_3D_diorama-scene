using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Visitors (CLAUDE.md 5.4): spawns one about every VisitorSpawnIntervalSeconds while playing, up to
    /// MaxVisitorsOnPath on the path, each asking for one fruit the player has planted and saying one of that fruit's
    /// virtue's situation lines. They walk in and wait with patience counting down. After time away, up to
    /// MaxOfflineArrivals are already waiting (ArriveAfterAway). Nobody is turned away: when patience runs out they go
    /// to the bench or the jungle queue (BenchSeating). Healed visitors leave. Pure C# on the GameClock; views follow
    /// the events and snapshots. Seated visitors are saved in GameState.Bench; path and off-screen visitors are not.
    /// Who gets a tonic is DeliveryTargeting's rule; this class only holds the reservation.
    /// </summary>
    public sealed class VisitorManager
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly HealService _heal;
        private readonly Random _random;
        private readonly BenchSeating _bench;
        // In arrival order, so the first waiting match on the path is always the longest-waiting visitor.
        private readonly List<Visitor> _visitors = new List<Visitor>(12);
        private readonly bool[] _spotTaken;
        private readonly List<string> _planted = new List<string>(9);
        private float _spawnTimer;
        private int _nextId = 1;

        public VisitorManager(GameState state, GameConfig config, TimeService time, EventBus events, HealService heal, Action markDirty, Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _heal = heal ?? throw new ArgumentNullException(nameof(heal));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            if (time == null) throw new ArgumentNullException(nameof(time));
            if (markDirty == null) throw new ArgumentNullException(nameof(markDirty));
            _bench = new BenchSeating(state, config, time, events, markDirty);
            _spotTaken = new bool[config.Farm.WaitingSpotCells.Count];
            // The first visitor comes as soon as something is planted, so a new farm never feels empty.
            _spawnTimer = Balance.VisitorSpawnIntervalSeconds;
            _bench.Restore(_visitors, () => _nextId++);
        }

        private BalanceConfig Balance => _config.Balance;

        /// <summary>While set (a daily wave waiting for the path to empty), no regular visitors are sent.</summary>
        public bool HoldSpawning { get; set; }

        /// <summary>Visitors can come: at least one fruit has been planted.</summary>
        public bool HasPlantedFruit
        {
            get
            {
                CollectPlantedFruits();
                return _planted.Count > 0;
            }
        }

        /// <summary>Path spots nobody is using.</summary>
        public int FreeSpots => Math.Min(Balance.MaxVisitorsOnPath, _spotTaken.Length) - PathCount;

        /// <summary>Everyone: on the path, on or walking to the bench, off-screen, and leaving.</summary>
        public int Count => _visitors.Count;
        public int SpotCount => _spotTaken.Length;
        public int SeatCount => _bench.SeatCount;
        /// <summary>Visitors holding a path spot (walking in, waiting, or healed and walking off).</summary>
        public int PathCount { get; private set; }
        /// <summary>Visitors holding a bench seat (walking to it or sitting).</summary>
        public int BenchCount => _bench.Count;
        /// <summary>Visitors walking back into the jungle or waiting there for a seat.</summary>
        public int OffscreenCount => _bench.OffscreenCount;

        /// <summary>The visitor at this position in arrival order (0 = longest here).</summary>
        public VisitorInfo GetAt(int index) => Info(_visitors[index]);

        /// <summary>The visitor holding a bench seat at this position, in the order they took their seats (0 = longest seated).</summary>
        public VisitorInfo GetBenchAt(int index) => Info(_bench.SeatedAt(index));

        public bool TryGet(int id, out VisitorInfo info)
        {
            Visitor v = Find(id);
            info = v != null ? Info(v) : default;
            return v != null;
        }

        /// <summary>Called on every GameClock tick with its interval.</summary>
        public void Tick(float dt)
        {
            // Forwards, in arrival order, so visitors who reach the bench or the jungle on the same tick queue in the
            // order they came.
            for (int i = 0; i < _visitors.Count; i++)
            {
                Visitor v = _visitors[i];
                v.Elapsed += dt;

                switch (v.Phase)
                {
                    case VisitorPhase.WalkingIn when v.Elapsed >= Balance.VisitorWalkInSeconds:
                        v.Elapsed -= Balance.VisitorWalkInSeconds;
                        v.Phase = VisitorPhase.Waiting;
                        break;
                    case VisitorPhase.Waiting when !v.Reserved && !v.RanOut && v.Elapsed >= PatienceOf(v):
                        RunOut(v);
                        break;
                    case VisitorPhase.WalkingToBench when v.Elapsed >= Balance.VisitorWalkToBenchSeconds:
                        _bench.SitDown(v);
                        break;
                    case VisitorPhase.WalkingAway when v.Elapsed >= Balance.VisitorWalkInSeconds:
                        _bench.ReachJungle(v);
                        break;
                    case VisitorPhase.Leaving when v.Elapsed >= Balance.VisitorWalkOutSeconds:
                        if (v.Spot >= 0)
                        {
                            _spotTaken[v.Spot] = false;
                            PathCount--;
                        }
                        _visitors.RemoveAt(i--);
                        _events.Publish(new VisitorLeft(v.Id, v.Spot));
                        break;
                }
            }

            _bench.FillFreeSeats();

            // Held at the interval while the path is full, so the next visitor comes as soon as a spot frees.
            _spawnTimer = Math.Min(_spawnTimer + dt, Balance.VisitorSpawnIntervalSeconds);
            if (!HoldSpawning && _spawnTimer >= Balance.VisitorSpawnIntervalSeconds && TrySpawn()) _spawnTimer = 0f;
        }

        /// <summary>
        /// Brings count visitors in together, walking in, each with this much patience (the daily wave, CLAUDE.md 5.10).
        /// All or nothing: needs count free path spots and something planted. Writes their ids into ids (length at
        /// least count) and returns how many came (count or 0). Restarts the spawn timer.
        /// </summary>
        public int SpawnGroup(int count, float patienceSeconds, int[] ids)
        {
            if (count <= 0 || FreeSpots < count) return 0;
            CollectPlantedFruits();
            if (_planted.Count == 0) return 0;
            for (int i = 0; i < count; i++)
            {
                // Walking in side by side, only the first speaks, so their lines never pile up.
                TrySpawn(patienceSeconds: patienceSeconds, speaks: i == 0);
                ids[i] = _nextId - 1;
            }
            _spawnTimer = 0f;
            return count;
        }

        /// <summary>
        /// Brings in a visitor now if there is room and something is planted (dev panel). Returns the new id, or -1.
        /// Counts as the timer's spawn, so the next regular visitor is a full interval away.
        /// </summary>
        public int SpawnNow()
        {
            if (!TrySpawn()) return -1;
            _spawnTimer = 0f;
            return _nextId - 1;
        }

        /// <summary>
        /// Time passed while the app was closed or in the background (CLAUDE.md 5.4): one visitor for every spawn interval
        /// away, at most MaxOfflineArrivals and the free path spots, already standing at their spots with full patience.
        /// Needs something planted. Restarts the spawn timer. Returns how many arrived.
        /// </summary>
        public int ArriveAfterAway(TimeSpan away)
        {
            double intervals = Math.Floor(away.TotalSeconds / Balance.VisitorSpawnIntervalSeconds);
            int due = (int)Math.Min(Balance.MaxOfflineArrivals, Math.Max(0d, intervals));
            int count = 0;
            while (count < due && TrySpawn(alreadyWaiting: true)) count++;
            if (count > 0) _spawnTimer = 0f;
            return count;
        }

        /// <summary>Dev: everyone waiting on the path without a tonic coming runs out of patience now. Returns how many.</summary>
        public int RunOutPatienceNow()
        {
            int count = 0;
            for (int i = 0; i < _visitors.Count; i++)
            {
                Visitor v = _visitors[i];
                if (v.Phase != VisitorPhase.Waiting || v.Reserved || v.RanOut) continue;
                RunOut(v);
                count++;
            }
            return count;
        }

        /// <summary>
        /// Picks who gets a tonic of this fruit by the targeting rule (DeliveryTargeting: path before bench, longest
        /// waiting first) and reserves them. Returns their id, or -1.
        /// </summary>
        public int Reserve(string fruitId)
        {
            int id = DeliveryTargeting.Pick(this, fruitId);
            return id >= 0 && TryReserve(id) ? id : -1;
        }

        /// <summary>
        /// Marks this visitor as having a tonic on its way, so no second one is sent and their patience holds. Only a
        /// visitor waiting at their spot or sitting on the bench can be reserved, and only once.
        /// </summary>
        public bool TryReserve(int visitorId)
        {
            Visitor v = Find(visitorId);
            if (v == null || v.Reserved || (v.Phase != VisitorPhase.Waiting && v.Phase != VisitorPhase.Sitting)) return false;
            v.Reserved = true;
            return true;
        }

        /// <summary>
        /// A tonic reached this visitor: heals them (seeds, counters, save; half seeds on the bench, more for a golden
        /// fruit) and sends them to the
        /// exit. A bench visitor gets up at once, so the seat is free for the next. Returns the seeds paid; 0 if they are
        /// gone or did not ask for this fruit.
        /// </summary>
        public int Deliver(int visitorId, string fruitId, bool golden = false)
        {
            Visitor v = Find(visitorId);
            if (v == null) return 0;
            v.Reserved = false;
            bool onBench = v.Phase == VisitorPhase.Sitting;
            if ((v.Phase != VisitorPhase.Waiting && !onBench) || !HealRules.Accepts(v.FruitId, fruitId)) return 0;

            int seeds = _heal.Heal(fruitId, visitorId, onBench, golden);
            if (onBench) _bench.StandUp(v);
            v.Phase = VisitorPhase.Leaving;
            v.Elapsed = 0f;
            _bench.FillFreeSeats();
            return seeds;
        }

        // Patience is gone: leave the path spot for the next visitor and go to the bench, or into the jungle if it is full.
        private void RunOut(Visitor v)
        {
            v.RanOut = true;
            _events.Publish(new VisitorPatienceRanOut(v.Id, v.Spot));
            _spotTaken[v.Spot] = false;
            v.Spot = -1;
            PathCount--;
            v.Elapsed = 0f;
            _bench.SeatOrSendAway(v);
        }

        private float PatienceOf(Visitor v) => v.PatienceSeconds > 0f ? v.PatienceSeconds : Balance.PatienceSeconds;

        private bool TrySpawn(bool alreadyWaiting = false, float patienceSeconds = 0f, bool speaks = true)
        {
            int max = Math.Min(Balance.MaxVisitorsOnPath, _spotTaken.Length);
            if (PathCount >= max) return false;
            int spot = Array.IndexOf(_spotTaken, false);
            if (spot < 0) return false;

            CollectPlantedFruits();
            if (_planted.Count == 0) return false;

            string fruitId = _planted[_random.Next(_planted.Count)];
            var v = new Visitor
            {
                Id = _nextId++,
                FruitId = fruitId,
                LineKey = speaks ? PickLine(fruitId) : null,
                Spot = spot,
                Phase = alreadyWaiting ? VisitorPhase.Waiting : VisitorPhase.WalkingIn,
                PatienceSeconds = patienceSeconds,
            };
            _spotTaken[spot] = true;
            PathCount++;
            _visitors.Add(v);
            _events.Publish(new VisitorArrived(v.Id, v.FruitId, v.Spot));
            return true;
        }

        // One of the lines of the virtue their fruit stands for (CLAUDE.md 5.4), or null if it has none.
        private string PickLine(string fruitId)
        {
            VirtueDefinition virtue = _config.FindFruit(fruitId)?.Virtue;
            if (virtue == null || virtue.SituationLineKeys.Count == 0) return null;
            return virtue.SituationLineKeys[_random.Next(virtue.SituationLineKeys.Count)];
        }

        // Only fruits the player has planted can be asked for (CLAUDE.md 5.4); each counts once however many trees.
        private void CollectPlantedFruits()
        {
            _planted.Clear();
            for (int i = 0; i < _state.Plots.Count; i++)
            {
                string fruit = _state.Plots[i].FruitId;
                if (!string.IsNullOrEmpty(fruit) && !_planted.Contains(fruit) && _config.FindFruit(fruit) != null)
                    _planted.Add(fruit);
            }
        }

        private Visitor Find(int id)
        {
            for (int i = 0; i < _visitors.Count; i++)
                if (_visitors[i].Id == id) return _visitors[i];
            return null;
        }

        private VisitorInfo Info(Visitor v)
        {
            float patience = v.Phase == VisitorPhase.WalkingIn ? 1f
                : v.Phase == VisitorPhase.Waiting ? Math.Max(0f, 1f - v.Elapsed / PatienceOf(v))
                : 0f;
            string definition = _config.Visitors.Count > 0 ? _config.Visitors[0].Id : null;
            return new VisitorInfo(v.Id, definition, v.FruitId, v.LineKey, v.Spot, v.Seat, v.Phase, v.Elapsed, patience, v.RanOut, v.Reserved, PatienceOf(v));
        }
    }
}
