using System;
using System.Globalization;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The Vibrancy Meter's owner (CLAUDE.md 5.7): every heal is checked against the stage thresholds; reaching a stage
    /// saves it, opens its plots (PlotManager) and announces StageReached, one stage at a time and in order. Fruits open
    /// by themselves because planting checks FruitDefinition.UnlockStage against the saved stage. Heals never lower the
    /// stage. At launch a save whose heal count is ahead of its stage catches up silently. Dispose to unsubscribe.
    /// </summary>
    public sealed class VibrancyManager : IDisposable
    {
        private readonly GameState _state;
        private readonly BalanceConfig _balance;
        private readonly EventBus _events;
        private readonly PlotManager _plots;
        private readonly Action _markDirty;

        public VibrancyManager(GameState state, GameConfig config, EventBus events, PlotManager plots, Action markDirty)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _balance = (config ?? throw new ArgumentNullException(nameof(config))).Balance;
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

            CatchUp();
            _events.Subscribe<VisitorHealed>(OnHealed);
        }

        public void Dispose() => _events.Unsubscribe<VisitorHealed>(OnHealed);

        public int Stage => _state.VibrancyStage;
        public int LastStage => _balance.StageThresholds.Count;
        public VibrancyProgress Progress => VibrancyRules.Progress(_balance, _state.HealedTotal, _state.VibrancyStage);

        /// <summary>
        /// Dev tool: jumps to a stage (clamped) and sets the heal count to that stage's threshold so the meter matches.
        /// Going up plays each stage reached as in a real game; going down greys the zones above again but keeps plots
        /// open (trees are permanent).
        /// </summary>
        public void SetStage(int stage)
        {
            stage = Math.Max(0, Math.Min(stage, LastStage));
            _state.HealedTotal = stage == 0 ? 0 : _balance.HealsToReach(stage);
            if (stage < _state.VibrancyStage) _state.VibrancyStage = stage;
            _markDirty();
            Advance();
        }

        private void OnHealed(VisitorHealed e) => Advance();

        private void Advance()
        {
            int target = VibrancyRules.StageFor(_balance, _state.HealedTotal);
            while (_state.VibrancyStage < target)
            {
                int stage = _state.VibrancyStage + 1;
                _state.VibrancyStage = stage;
                _plots.ApplyStageUnlocks();
                _markDirty();
                _events.Publish(new StageReached(stage, stage == LastStage));
                Telemetry.Log("stage_reached", stage.ToString(CultureInfo.InvariantCulture));
            }
            _events.Publish(new VibrancyChanged(_state.HealedTotal, _state.VibrancyStage));
        }

        // A save can be ahead of its stage (an older build, or a crash between the heal and the save); no fanfare at launch.
        private void CatchUp()
        {
            int target = VibrancyRules.StageFor(_balance, _state.HealedTotal);
            if (target > _state.VibrancyStage)
            {
                Log.Info($"VibrancyManager: {_state.HealedTotal} heals, stage {_state.VibrancyStage} → {target} at launch.");
                _state.VibrancyStage = target;
                _markDirty();
            }
            _plots.ApplyStageUnlocks();
        }
    }
}
