using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Visitors (CLAUDE.md 5.4): spawns one about every VisitorSpawnIntervalSeconds while playing, up to
    /// MaxVisitorsOnPath on the path, each asking for one fruit the player has planted. They walk in and wait with
    /// patience counting down. Nobody is turned away: when patience runs out they walk to the bench and sit, still
    /// asking; with the bench full they wait off-screen in the jungle until a seat frees. Healed visitors leave.
    /// Pure C# on the GameClock; views follow the events and snapshots. Seated visitors are saved in GameState.Bench;
    /// path and off-screen visitors are not (offline arrivals, W2·06).
    /// </summary>
    public sealed class VisitorManager
    {
        private sealed class Visitor
        {
            public int Id;
            public string FruitId;
            public int Spot = -1;
            public int Seat = -1;
            public VisitorPhase Phase;
            public float Elapsed;
            public bool Reserved;
            public bool RanOut;
            // Their entry in GameState.Bench once they have sat down; removed when healed.
            public BenchVisitorState Saved;
        }

        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly TimeService _time;
        private readonly EventBus _events;
        private readonly HealService _heal;
        private readonly Action _markDirty;
        private readonly Random _random;
        // In arrival order, so the first waiting match on the path is always the longest-waiting visitor.
        private readonly List<Visitor> _visitors = new List<Visitor>(12);
        // Seated (or walking to their seat) in the order they took the seat: longest on the bench first.
        private readonly List<Visitor> _benchOrder = new List<Visitor>(4);
        // Waiting in the jungle for a seat, first come first seated.
        private readonly List<Visitor> _offscreen = new List<Visitor>(4);
        private readonly bool[] _spotTaken;
        private readonly bool[] _seatTaken;
        private readonly List<string> _planted = new List<string>(9);
        private float _spawnTimer;
        private int _nextId = 1;

        public VisitorManager(GameState state, GameConfig config, TimeService time, EventBus events, HealService heal, Action markDirty, Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _heal = heal ?? throw new ArgumentNullException(nameof(heal));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _spotTaken = new bool[config.Farm.WaitingSpotCells.Count];
            _seatTaken = new bool[Balance.BenchSeats];
            // The first visitor comes as soon as something is planted, so a new farm never feels empty.
            _spawnTimer = Balance.VisitorSpawnIntervalSeconds;
            RestoreBench();
        }

        private BalanceConfig Balance => _config.Balance;

        /// <summary>Everyone: on the path, on or walking to the bench, off-screen, and leaving.</summary>
        public int Count => _visitors.Count;
        public int SpotCount => _spotTaken.Length;
        public int SeatCount => _seatTaken.Length;
        /// <summary>Visitors holding a path spot (walking in, waiting, or healed and walking off).</summary>
        public int PathCount { get; private set; }
        /// <summary>Visitors holding a bench seat (walking to it or sitting).</summary>
        public int BenchCount => _benchOrder.Count;
        /// <summary>Visitors walking back into the jungle or waiting there for a seat.</summary>
        public int OffscreenCount { get; private set; }

        /// <summary>The visitor at this position in arrival order (0 = longest here).</summary>
        public VisitorInfo GetAt(int index) => Info(_visitors[index]);

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
                    case VisitorPhase.Waiting when !v.Reserved && !v.RanOut && v.Elapsed >= Balance.PatienceSeconds:
                        RunOut(v);
                        break;
                    case VisitorPhase.WalkingToBench when v.Elapsed >= Balance.VisitorWalkToBenchSeconds:
                        SitDown(v);
                        break;
                    case VisitorPhase.WalkingAway when v.Elapsed >= Balance.VisitorWalkInSeconds:
                        v.Phase = VisitorPhase.Offscreen;
                        v.Elapsed = 0f;
                        _offscreen.Add(v);
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

            FillFreeSeats();

            // Held at the interval while the path is full, so the next visitor comes as soon as a spot frees.
            _spawnTimer = Math.Min(_spawnTimer + dt, Balance.VisitorSpawnIntervalSeconds);
            if (_spawnTimer >= Balance.VisitorSpawnIntervalSeconds && TrySpawn()) _spawnTimer = 0f;
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
        /// Picks who gets a tonic of this fruit (CLAUDE.md 5.5): the longest-waiting visitor on the path who asked for it
        /// and has no tonic coming yet, else the longest-seated such visitor on the bench. Marks them so the next harvest
        /// goes to someone else. Returns their id, or -1.
        /// </summary>
        public int Reserve(string fruitId)
        {
            for (int i = 0; i < _visitors.Count; i++)
            {
                Visitor v = _visitors[i];
                if (v.Phase != VisitorPhase.Waiting || v.Reserved || v.FruitId != fruitId) continue;
                v.Reserved = true;
                return v.Id;
            }
            for (int i = 0; i < _benchOrder.Count; i++)
            {
                Visitor v = _benchOrder[i];
                if (v.Phase != VisitorPhase.Sitting || v.Reserved || v.FruitId != fruitId) continue;
                v.Reserved = true;
                return v.Id;
            }
            return -1;
        }

        /// <summary>
        /// A tonic reached this visitor: heals them (seeds, counters, save; half seeds on the bench) and sends them to the
        /// exit. A bench visitor gets up at once, so the seat is free for the next. Returns the seeds paid; 0 if they are
        /// gone or did not ask for this fruit.
        /// </summary>
        public int Deliver(int visitorId, string fruitId)
        {
            Visitor v = Find(visitorId);
            if (v == null) return 0;
            v.Reserved = false;
            bool onBench = v.Phase == VisitorPhase.Sitting;
            if ((v.Phase != VisitorPhase.Waiting && !onBench) || !HealRules.Accepts(v.FruitId, fruitId)) return 0;

            int seeds = _heal.Heal(fruitId, visitorId, onBench);
            if (onBench) StandUp(v);
            v.Phase = VisitorPhase.Leaving;
            v.Elapsed = 0f;
            FillFreeSeats();
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

            int seat = Array.IndexOf(_seatTaken, false);
            if (seat >= 0) TakeSeat(v, seat);
            else
            {
                v.Phase = VisitorPhase.WalkingAway;
                OffscreenCount++;
            }
        }

        private void TakeSeat(Visitor v, int seat)
        {
            _seatTaken[seat] = true;
            v.Seat = seat;
            v.Phase = VisitorPhase.WalkingToBench;
            v.Elapsed = 0f;
            _benchOrder.Add(v);
        }

        private void SitDown(Visitor v)
        {
            v.Phase = VisitorPhase.Sitting;
            v.Elapsed = 0f;
            if (v.Saved == null)
            {
                v.Saved = new BenchVisitorState(v.FruitId, _time.NowUtcMs);
                _state.Bench.Add(v.Saved);
                _markDirty();
            }
            _events.Publish(new VisitorSatDown(v.Id, v.Seat));
        }

        private void StandUp(Visitor v)
        {
            _seatTaken[v.Seat] = false;
            v.Seat = -1;
            _benchOrder.Remove(v);
            if (v.Saved != null)
            {
                _state.Bench.Remove(v.Saved);
                v.Saved = null;
                _markDirty();
            }
        }

        // Whoever has waited longest in the jungle walks to the next free seat.
        private void FillFreeSeats()
        {
            while (_offscreen.Count > 0)
            {
                int seat = Array.IndexOf(_seatTaken, false);
                if (seat < 0) return;
                Visitor v = _offscreen[0];
                _offscreen.RemoveAt(0);
                OffscreenCount--;
                TakeSeat(v, seat);
            }
        }

        // Seated visitors from the save sit down again in the order they first sat; any beyond the seats (fewer seats
        // in a newer balance) wait off-screen, still saved. Unknown fruits (removed from the game) are dropped.
        private void RestoreBench()
        {
            List<BenchVisitorState> saved = _state.Bench;
            for (int i = saved.Count - 1; i >= 0; i--)
            {
                if (saved[i] != null && _config.FindFruit(saved[i].FruitId) != null) continue;
                Log.Warn($"VisitorManager: dropped a saved bench visitor with unknown fruit '{saved[i]?.FruitId}'.");
                saved.RemoveAt(i);
                _markDirty();
            }

            var ordered = new List<BenchVisitorState>(saved);
            // Insertion sort is stable, so equal times keep their saved order.
            for (int i = 1; i < ordered.Count; i++)
            {
                BenchVisitorState s = ordered[i];
                int j = i - 1;
                for (; j >= 0 && ordered[j].ArrivedUtc > s.ArrivedUtc; j--) ordered[j + 1] = ordered[j];
                ordered[j + 1] = s;
            }

            for (int i = 0; i < ordered.Count; i++)
            {
                var v = new Visitor { Id = _nextId++, FruitId = ordered[i].FruitId, RanOut = true, Saved = ordered[i] };
                _visitors.Add(v);
                int seat = Array.IndexOf(_seatTaken, false);
                if (seat >= 0)
                {
                    TakeSeat(v, seat);
                    v.Phase = VisitorPhase.Sitting;
                }
                else
                {
                    v.Phase = VisitorPhase.Offscreen;
                    _offscreen.Add(v);
                    OffscreenCount++;
                }
            }
        }

        private bool TrySpawn()
        {
            int max = Math.Min(Balance.MaxVisitorsOnPath, _spotTaken.Length);
            if (PathCount >= max) return false;
            int spot = Array.IndexOf(_spotTaken, false);
            if (spot < 0) return false;

            CollectPlantedFruits();
            if (_planted.Count == 0) return false;

            var v = new Visitor
            {
                Id = _nextId++,
                FruitId = _planted[_random.Next(_planted.Count)],
                Spot = spot,
                Phase = VisitorPhase.WalkingIn,
            };
            _spotTaken[spot] = true;
            PathCount++;
            _visitors.Add(v);
            _events.Publish(new VisitorArrived(v.Id, v.FruitId, v.Spot));
            return true;
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
                : v.Phase == VisitorPhase.Waiting ? Math.Max(0f, 1f - v.Elapsed / Balance.PatienceSeconds)
                : 0f;
            string definition = _config.Visitors.Count > 0 ? _config.Visitors[0].Id : null;
            return new VisitorInfo(v.Id, definition, v.FruitId, v.Spot, v.Seat, v.Phase, v.Elapsed, patience, v.RanOut, v.Reserved);
        }
    }
}
