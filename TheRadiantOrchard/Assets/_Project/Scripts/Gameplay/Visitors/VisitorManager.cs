using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Visitors on the path (CLAUDE.md 5.4): spawns one about every VisitorSpawnIntervalSeconds while playing, up to
    /// MaxVisitorsOnPath and the number of waiting spots, each asking for one fruit the player has planted. Visitors
    /// walk in, wait with patience counting down, and leave once healed. Pure C# on the GameClock; views follow the
    /// events and snapshots. Path visitors are not saved: the path clears when the app closes (offline arrivals, W2·06).
    /// The bench (W2·05) takes visitors whose patience ran out.
    /// </summary>
    public sealed class VisitorManager
    {
        private sealed class Visitor
        {
            public int Id;
            public string FruitId;
            public int Spot;
            public VisitorPhase Phase;
            public float Elapsed;
            public bool Reserved;
            public bool RanOut;
        }

        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly HealService _heal;
        private readonly Random _random;
        // In arrival order, so the first match is always the longest-waiting visitor.
        private readonly List<Visitor> _visitors = new List<Visitor>(8);
        private readonly bool[] _spotTaken;
        private readonly List<string> _planted = new List<string>(9);
        private float _spawnTimer;
        private int _nextId = 1;

        public VisitorManager(GameState state, GameConfig config, EventBus events, HealService heal, Random random)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _heal = heal ?? throw new ArgumentNullException(nameof(heal));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _spotTaken = new bool[config.Farm.WaitingSpotCells.Count];
            // The first visitor comes as soon as something is planted, so a new farm never feels empty.
            _spawnTimer = Balance.VisitorSpawnIntervalSeconds;
        }

        private BalanceConfig Balance => _config.Balance;

        public int Count => _visitors.Count;
        public int SpotCount => _spotTaken.Length;

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
            for (int i = _visitors.Count - 1; i >= 0; i--)
            {
                Visitor v = _visitors[i];
                v.Elapsed += dt;

                if (v.Phase == VisitorPhase.WalkingIn && v.Elapsed >= Balance.VisitorWalkInSeconds)
                {
                    v.Elapsed -= Balance.VisitorWalkInSeconds;
                    v.Phase = VisitorPhase.Waiting;
                }

                if (v.Phase == VisitorPhase.Waiting && !v.Reserved && !v.RanOut && v.Elapsed >= Balance.PatienceSeconds)
                {
                    v.RanOut = true;
                    _events.Publish(new VisitorPatienceRanOut(v.Id, v.Spot));
                }
                else if (v.Phase == VisitorPhase.Leaving && v.Elapsed >= Balance.VisitorWalkOutSeconds)
                {
                    _spotTaken[v.Spot] = false;
                    _visitors.RemoveAt(i);
                    _events.Publish(new VisitorLeft(v.Id, v.Spot));
                }
            }

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

        /// <summary>
        /// Picks who gets a tonic of this fruit: the longest-waiting visitor at their spot who asked for it and has no
        /// tonic coming yet. Marks them so the next harvest goes to someone else. Returns their id, or -1.
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
            return -1;
        }

        /// <summary>
        /// A tonic reached this visitor: heals them (seeds, counters, save) and sends them to the exit. Returns the seeds
        /// paid; 0 if they are gone or did not ask for this fruit.
        /// </summary>
        public int Deliver(int visitorId, string fruitId)
        {
            Visitor v = Find(visitorId);
            if (v == null) return 0;
            v.Reserved = false;
            if (v.Phase != VisitorPhase.Waiting || !HealRules.Accepts(v.FruitId, fruitId)) return 0;

            int seeds = _heal.Heal(fruitId, visitorId);
            v.Phase = VisitorPhase.Leaving;
            v.Elapsed = 0f;
            return seeds;
        }

        private bool TrySpawn()
        {
            int max = Math.Min(Balance.MaxVisitorsOnPath, _spotTaken.Length);
            if (_visitors.Count >= max) return false;
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
                : v.Phase == VisitorPhase.Leaving ? 0f
                : Math.Max(0f, 1f - v.Elapsed / Balance.PatienceSeconds);
            string definition = _config.Visitors.Count > 0 ? _config.Visitors[0].Id : null;
            return new VisitorInfo(v.Id, definition, v.FruitId, v.Spot, v.Phase, v.Elapsed, patience, v.RanOut, v.Reserved);
        }
    }
}
