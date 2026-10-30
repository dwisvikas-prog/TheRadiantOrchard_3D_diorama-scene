using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Heals a visitor (CLAUDE.md 5.6): pays seeds, counts the heal towards the progress bar, quiz and mini-events,
    /// marks the save dirty and announces it. Presentation plays the colour sweep and virtue pop-up from VisitorHealed.
    /// </summary>
    public sealed class HealService
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly Action _markDirty;

        public HealService(GameState state, GameConfig config, EventBus events, Action markDirty)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
        }

        /// <summary>Heals with this fruit's tonic. Returns the seeds paid (0 for an unknown fruit).</summary>
        public int Heal(string fruitId)
        {
            FruitDefinition fruit = _config.FindFruit(fruitId);
            if (fruit == null)
            {
                Log.Warn($"HealService: unknown fruit '{fruitId}'; no heal.");
                return 0;
            }

            int seeds = HealRules.SeedsFor(_config.Balance);
            _state.Seeds += seeds;
            _state.HealedTotal++;
            _state.HealedSinceQuiz++;
            _state.HealedSinceEvent++;
            _markDirty();

            _events.Publish(new SeedsChanged(_state.Seeds, seeds));
            _events.Publish(new VisitorHealed(fruit.Id, fruit.VirtueNameKey, seeds, _state.HealedTotal));
            Telemetry.Log("visitor_healed", fruit.Id);
            return seeds;
        }
    }
}
