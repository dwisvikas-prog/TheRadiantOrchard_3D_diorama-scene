using System;
using System.Collections.Generic;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Mini-events (CLAUDE.md 5.11): heals count up in GameState.HealedSinceEvent (HealService); at MiniEventEveryHeals
    /// one event is picked (MiniEventRules: weighted, not the last one, only events that can do something now), handed
    /// to the effect for its kind, and announced (MiniEventStarted, telemetry "event_started"); the count starts again.
    /// When nothing can start, the count waits and the next heal tries again. Data-driven: events are
    /// MiniEventDefinition assets on GameConfig. Dispose to unsubscribe.
    /// </summary>
    public sealed class MiniEventManager : IDisposable
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly Action _markDirty;
        private readonly Random _random;
        private readonly Dictionary<MiniEventKind, IMiniEventEffect> _effects = new Dictionary<MiniEventKind, IMiniEventEffect>();
        private readonly Func<MiniEventDefinition, bool> _canStart;

        public MiniEventManager(GameState state, GameConfig config, EventBus events, Action markDirty, Random random, params IMiniEventEffect[] effects)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            foreach (IMiniEventEffect effect in effects) _effects[effect.Kind] = effect;
            _canStart = CanStart;
            _events.Subscribe<VisitorHealed>(OnHealed);
        }

        public void Dispose() => _events.Unsubscribe<VisitorHealed>(OnHealed);

        /// <summary>The event that ran last (in this session), or null.</summary>
        public string LastId { get; private set; }

        public MiniEventDefinition Find(string id)
        {
            foreach (MiniEventDefinition e in _config.MiniEvents)
                if (e != null && e.Id == id) return e;
            return null;
        }

        public bool CanStart(MiniEventDefinition definition) =>
            definition != null && _effects.TryGetValue(definition.Kind, out IMiniEventEffect effect) && effect.CanStart(definition);

        /// <summary>Dev tool: starts this event now if it can do something. The heal count is left alone.</summary>
        public bool StartNow(string id)
        {
            MiniEventDefinition definition = Find(id);
            if (!CanStart(definition)) return false;
            Start(definition);
            return true;
        }

        private void OnHealed(VisitorHealed e)
        {
            if (_state.HealedSinceEvent < _config.Balance.MiniEventEveryHeals) return;
            MiniEventDefinition picked = MiniEventRules.Pick(_config.MiniEvents, _canStart, LastId, _random);
            if (picked == null) return;
            _state.HealedSinceEvent = 0;
            _markDirty();
            Start(picked);
        }

        private void Start(MiniEventDefinition definition)
        {
            _effects[definition.Kind].Start(definition);
            LastId = definition.Id;
            _events.Publish(new MiniEventStarted(definition.Id));
            Telemetry.Log("event_started", definition.Id);
        }
    }
}
