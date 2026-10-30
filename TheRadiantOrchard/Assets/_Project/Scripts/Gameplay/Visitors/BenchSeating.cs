using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The bench and the jungle queue (CLAUDE.md 5.4, "no one is turned away"): visitors whose patience ran out take the
    /// lowest free seat, or wait off-screen first come first seated until one frees. Seated visitors are saved in
    /// GameState.Bench and sit down again on launch. Part of VisitorManager, which owns the visitors and their phases.
    /// </summary>
    internal sealed class BenchSeating
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly TimeService _time;
        private readonly EventBus _events;
        private readonly Action _markDirty;
        private readonly bool[] _seatTaken;
        // Seated (or walking to their seat) in the order they took the seat: longest on the bench first.
        private readonly List<Visitor> _seated = new List<Visitor>(4);
        // Waiting in the jungle for a seat, first come first seated.
        private readonly List<Visitor> _queue = new List<Visitor>(4);

        public BenchSeating(GameState state, GameConfig config, TimeService time, EventBus events, Action markDirty)
        {
            _state = state;
            _config = config;
            _time = time;
            _events = events;
            _markDirty = markDirty;
            _seatTaken = new bool[config.Balance.BenchSeats];
        }

        public int SeatCount => _seatTaken.Length;
        /// <summary>Visitors holding a seat (walking to it or sitting).</summary>
        public int Count => _seated.Count;
        /// <summary>Visitors walking back into the jungle or waiting there for a seat.</summary>
        public int OffscreenCount { get; private set; }

        /// <summary>The seated visitor at this position in the order they took their seats (0 = longest on the bench).</summary>
        public Visitor SeatedAt(int index) => _seated[index];

        /// <summary>Patience ran out: the lowest free seat, or back into the jungle when the bench is full.</summary>
        public void SeatOrSendAway(Visitor v)
        {
            int seat = Array.IndexOf(_seatTaken, false);
            if (seat >= 0) TakeSeat(v, seat);
            else
            {
                v.Phase = VisitorPhase.WalkingAway;
                OffscreenCount++;
            }
        }

        /// <summary>They are out of sight in the jungle and join the queue for a seat.</summary>
        public void ReachJungle(Visitor v)
        {
            v.Phase = VisitorPhase.Offscreen;
            v.Elapsed = 0f;
            _queue.Add(v);
        }

        public void SitDown(Visitor v)
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

        /// <summary>Healed on the bench: the seat frees at once and they leave the save.</summary>
        public void StandUp(Visitor v)
        {
            _seatTaken[v.Seat] = false;
            v.Seat = -1;
            _seated.Remove(v);
            if (v.Saved != null)
            {
                _state.Bench.Remove(v.Saved);
                v.Saved = null;
                _markDirty();
            }
        }

        /// <summary>Whoever has waited longest in the jungle walks to the next free seat.</summary>
        public void FillFreeSeats()
        {
            while (_queue.Count > 0)
            {
                int seat = Array.IndexOf(_seatTaken, false);
                if (seat < 0) return;
                Visitor v = _queue[0];
                _queue.RemoveAt(0);
                OffscreenCount--;
                TakeSeat(v, seat);
            }
        }

        /// <summary>
        /// Seated visitors from the save sit down again in the order they first sat; any beyond the seats (fewer seats in
        /// a newer balance) wait off-screen, still saved. Unknown fruits (removed from the game) are dropped.
        /// Adds them to visitors, taking ids from nextId.
        /// </summary>
        public void Restore(List<Visitor> visitors, Func<int> nextId)
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
                var v = new Visitor { Id = nextId(), FruitId = ordered[i].FruitId, RanOut = true, Saved = ordered[i] };
                visitors.Add(v);
                int seat = Array.IndexOf(_seatTaken, false);
                if (seat >= 0)
                {
                    TakeSeat(v, seat);
                    v.Phase = VisitorPhase.Sitting;
                }
                else
                {
                    v.Phase = VisitorPhase.Offscreen;
                    _queue.Add(v);
                    OffscreenCount++;
                }
            }
        }

        private void TakeSeat(Visitor v, int seat)
        {
            _seatTaken[seat] = true;
            v.Seat = seat;
            v.Phase = VisitorPhase.WalkingToBench;
            v.Elapsed = 0f;
            _seated.Add(v);
        }
    }
}
