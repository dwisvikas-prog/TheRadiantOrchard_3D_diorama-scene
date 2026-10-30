using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Timed seed multipliers (ENGINEERING_GUIDE 13: the generic Boost seam). Golden Harvest (CLAUDE.md 5.8) and the
    /// Golden Hour mini-event (5.11) are boosts; Phase 2's power-up can be one too. Boosts with the same id do not stack:
    /// starting one again restarts its time. Runs on play time (GameClock) and is not saved, so a boost ends when the app
    /// closes. Announces BoostStarted / BoostEnded for the golden glow.
    /// </summary>
    public sealed class BoostService
    {
        /// <summary>The quiz reward: double seeds for GoldenHarvestSeconds.</summary>
        public const string GoldenHarvest = "golden_harvest";

        private struct Boost
        {
            public string Id;
            public float Multiplier;
            public float SecondsLeft;
        }

        private readonly EventBus _events;
        private readonly List<Boost> _active = new List<Boost>(2);

        public BoostService(EventBus events) => _events = events ?? throw new ArgumentNullException(nameof(events));

        public int ActiveCount => _active.Count;

        /// <summary>All active boosts multiplied together (1 with none).</summary>
        public float SeedMultiplier
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < _active.Count; i++) m *= _active[i].Multiplier;
                return m;
            }
        }

        public void Start(string id, float multiplier, float seconds)
        {
            int i = IndexOf(id);
            var boost = new Boost { Id = id, Multiplier = multiplier, SecondsLeft = seconds };
            if (i >= 0) _active[i] = boost;
            else _active.Add(boost);
            Telemetry.Log("boost_started", id);
            _events.Publish(new BoostStarted(id, multiplier, seconds));
        }

        public bool IsActive(string id) => IndexOf(id) >= 0;

        public float SecondsLeft(string id)
        {
            int i = IndexOf(id);
            return i >= 0 ? _active[i].SecondsLeft : 0f;
        }

        /// <summary>Called on every GameClock tick with its interval.</summary>
        public void Tick(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Boost boost = _active[i];
                boost.SecondsLeft -= dt;
                if (boost.SecondsLeft > 0f)
                {
                    _active[i] = boost;
                    continue;
                }
                _active.RemoveAt(i);
                _events.Publish(new BoostEnded(boost.Id));
            }
        }

        private int IndexOf(string id)
        {
            for (int i = 0; i < _active.Count; i++)
                if (_active[i].Id == id) return i;
            return -1;
        }
    }
}
