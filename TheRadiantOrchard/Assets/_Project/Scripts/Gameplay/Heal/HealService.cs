using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Heals a visitor (CLAUDE.md 5.6): pays seeds (HealRules: bench, golden fruit, boosts, combo), counts the heal
    /// towards the progress bar, quiz and mini-events, marks the save dirty and announces it. Presentation plays the
    /// colour sweep, virtue and seed pop-ups and the combo counter from VisitorHealed.
    /// </summary>
    public sealed class HealService
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly Action _markDirty;
        private readonly TimeService _time;
        private readonly BoostService _boosts;
        private readonly ComboTracker _combo;

        /// <param name="time">Clock for combos; without one every heal is a single (no combo).</param>
        /// <param name="boosts">Active seed boosts (Golden Harvest); without it there are none.</param>
        public HealService(GameState state, GameConfig config, EventBus events, Action markDirty, TimeService time = null, BoostService boosts = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _time = time;
            _boosts = boosts;
            _combo = new ComboTracker(config.Balance.ComboWindowSeconds);
        }

        /// <summary>Heals with this fruit's tonic. Returns the seeds paid (0 for an unknown fruit).</summary>
        /// <param name="visitorId">Who was healed, for the colour sweep and pop-up; -1 for dev heals, which never build a combo.</param>
        /// <param name="fromBench">They were on the bench: half seeds (CLAUDE.md 5.4).</param>
        /// <param name="golden">A golden fruit's tonic (5.12).</param>
        public int Heal(string fruitId, int visitorId = -1, bool fromBench = false, bool golden = false)
        {
            FruitDefinition fruit = _config.FindFruit(fruitId);
            if (fruit == null)
            {
                Log.Warn($"HealService: unknown fruit '{fruitId}'; no heal.");
                return 0;
            }

            int combo = visitorId >= 0 && _time != null ? _combo.Register(_time.NowUtcMs) : 1;
            float boost = _boosts != null ? _boosts.SeedMultiplier : 1f;
            int seeds = HealRules.SeedsFor(_config.Balance, fromBench, golden, boost, combo);
            _state.Seeds += seeds;
            _state.HealedTotal++;
            _state.HealedSinceQuiz++;
            _state.HealedSinceEvent++;
            _markDirty();

            _events.Publish(new SeedsChanged(_state.Seeds, seeds));
            _events.Publish(new VisitorHealed(visitorId, fruit.Id, fruit.VirtueNameKey, seeds, _state.HealedTotal,
                HealRules.BaseSeeds(_config.Balance, fromBench), HealRules.ComboMultiplier(_config.Balance, combo), golden, fromBench));
            Telemetry.Log("visitor_healed", fruit.Id);
            return seeds;
        }
    }
}
