using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Owns the plots in GameState: planting, harvesting, regrowth, ripening and opening plots by stage.
    /// Every change marks the save dirty and is announced on the EventBus; views never change plots themselves.
    /// </summary>
    public sealed class PlotManager
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly TimeService _time;
        private readonly EventBus _events;
        private readonly Action _markDirty;
        private readonly TreeStage[] _lastStages;

        public PlotManager(GameState state, GameConfig config, TimeService time, EventBus events, Action markDirty)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));

            _lastStages = new TreeStage[_state.Plots.Count];
            for (int i = 0; i < _lastStages.Length; i++)
                _lastStages[i] = StageOf(i, _time.NowUtcMs);
        }

        public int Count => _state.Plots.Count;

        /// <summary>
        /// Asked before a gesture picks a fruit: false keeps it on the tree (DeliveryService.CanTake: nobody wants it and
        /// its basket is full). Null lets every harvest through.
        /// </summary>
        public Func<string, bool> HarvestGate { get; set; }

        public PlotInfo Get(int index)
        {
            if (!IsValid(index)) return default;
            PlotState plot = _state.Plots[index];
            FruitDefinition fruit = FruitOn(plot);
            long now = _time.NowUtcMs;
            return new PlotInfo(
                index,
                plot.Unlocked,
                plot.FruitId,
                StageOf(index, now),
                GrowthCalculator.SecondsUntilRipe(plot, fruit, now),
                (float)GrowthCalculator.Progress(plot, fruit, now));
        }

        /// <summary>Whether this fruit could be planted here now, and if not, why (for greying out menu options).</summary>
        public PlantResult CanPlant(int index, FruitDefinition fruit)
        {
            if (!IsValid(index)) return PlantResult.InvalidPlot;
            PlotState plot = _state.Plots[index];
            if (!plot.Unlocked) return PlantResult.PlotLocked;
            if (plot.IsPlanted) return PlantResult.PlotOccupied;
            if (fruit == null) return PlantResult.UnknownFruit;
            if (fruit.UnlockStage > _state.VibrancyStage) return PlantResult.FruitLocked;
            if (_state.Seeds < fruit.PlantCost) return PlantResult.NotEnoughSeeds;
            return PlantResult.Planted;
        }

        public PlantResult TryPlant(int index, string fruitId)
        {
            FruitDefinition fruit = _config.FindFruit(fruitId);
            PlantResult check = CanPlant(index, fruit);
            if (check != PlantResult.Planted) return check;

            PlotState plot = _state.Plots[index];
            if (fruit.PlantCost > 0)
            {
                _state.Seeds -= fruit.PlantCost;
                _events.Publish(new SeedsChanged(_state.Seeds, -fruit.PlantCost));
            }

            plot.FruitId = fruit.Id;
            plot.PlantedUtc = _time.NowUtcMs;
            plot.LastHarvestUtc = 0;
            Changed(index);
            return PlantResult.Planted;
        }

        /// <summary>Picks the fruit of a ripe tree and starts its regrowth. False if the tree is not ripe.</summary>
        public bool TryHarvest(int index, out FruitDefinition fruit)
        {
            fruit = null;
            if (!IsValid(index)) return false;
            PlotState plot = _state.Plots[index];
            if (StageOf(index, _time.NowUtcMs) != TreeStage.Ripe) return false;

            fruit = FruitOn(plot);
            plot.LastHarvestUtc = _time.NowUtcMs;
            Changed(index);
            _events.Publish(new FruitHarvested(index, fruit.Id));
            return true;
        }

        /// <summary>
        /// A harvest gesture on a plot (HarvestRules): the fruit's own gesture picks a ripe tree; another gesture on a
        /// ripe tree raises HarvestRejected. Anything else is ignored.
        /// </summary>
        public HarvestJudgement HandleGesture(int index, GestureType gesture)
        {
            if (!IsValid(index)) return HarvestJudgement.Ignore;
            PlotInfo info = Get(index);
            return Apply(index, HarvestRules.Judge(info, _config.FindFruit(info.FruitId), gesture));
        }

        /// <summary>A single tap on a plot that is not empty (HarvestRules.JudgeTap).</summary>
        public HarvestJudgement HandleTap(int index)
        {
            if (!IsValid(index)) return HarvestJudgement.Ignore;
            PlotInfo info = Get(index);
            return Apply(index, HarvestRules.JudgeTap(info, _config.FindFruit(info.FruitId)));
        }

        private HarvestJudgement Apply(int index, HarvestJudgement judgement)
        {
            if (judgement == HarvestJudgement.Harvest && HarvestGate != null && !HarvestGate(_state.Plots[index].FruitId))
            {
                _events.Publish(new HarvestHeld(index, _state.Plots[index].FruitId));
                return HarvestJudgement.Held;
            }
            if (judgement == HarvestJudgement.Harvest && !TryHarvest(index, out _)) return HarvestJudgement.Ignore;
            if (judgement == HarvestJudgement.WrongGesture) _events.Publish(new HarvestRejected(index, _state.Plots[index].FruitId));
            return judgement;
        }

        /// <summary>Makes a planted tree ripe now (tutorial, Blessing Rain, dev tools). False if nothing is planted.</summary>
        public bool RipenNow(int index)
        {
            if (!IsValid(index)) return false;
            PlotState plot = _state.Plots[index];
            FruitDefinition fruit = FruitOn(plot);
            if (fruit == null) return false;

            long ripeStart = _time.NowUtcMs - fruit.GrowSeconds * 1000L;
            if (plot.LastHarvestUtc > 0) plot.LastHarvestUtc = ripeStart;
            else plot.PlantedUtc = ripeStart;
            Changed(index);
            return true;
        }

        /// <summary>Ripens every planted tree. Returns how many were ripened.</summary>
        public int RipenAll()
        {
            int count = 0;
            for (int i = 0; i < Count; i++)
                if (StageOf(i, _time.NowUtcMs) != TreeStage.Ripe && RipenNow(i)) count++;
            return count;
        }

        /// <summary>Opens the plots the current vibrancy stage allows (BalanceConfig.PlotsOpenAtStage).</summary>
        public void ApplyStageUnlocks()
        {
            int open = Math.Min(Count, _config.Balance.PlotsOpenAt(_state.VibrancyStage));
            bool any = false;
            for (int i = 0; i < open; i++)
            {
                if (_state.Plots[i].Unlocked) continue;
                _state.Plots[i].Unlocked = true;
                any = true;
                _events.Publish(new PlotUnlocked(i));
            }
            if (any) _markDirty();
        }

        /// <summary>Called by the GameClock: announces trees that grew or ripened since the last tick.</summary>
        public void Tick()
        {
            long now = _time.NowUtcMs;
            for (int i = 0; i < _lastStages.Length; i++)
            {
                TreeStage stage = StageOf(i, now);
                if (stage == _lastStages[i]) continue;
                _lastStages[i] = stage;
                _events.Publish(new PlotStageChanged(i, stage));
            }
        }

        private void Changed(int index)
        {
            _markDirty();
            TreeStage stage = StageOf(index, _time.NowUtcMs);
            _lastStages[index] = stage;
            _events.Publish(new PlotStageChanged(index, stage));
        }

        private TreeStage StageOf(int index, long now)
        {
            PlotState plot = _state.Plots[index];
            if (!plot.Unlocked) return TreeStage.Empty;
            return GrowthCalculator.StageAt(plot, FruitOn(plot), now, _config.Balance.SaplingFraction);
        }

        private FruitDefinition FruitOn(PlotState plot) => plot.IsPlanted ? _config.FindFruit(plot.FruitId) : null;

        private bool IsValid(int index) => index >= 0 && index < _state.Plots.Count;
    }
}
