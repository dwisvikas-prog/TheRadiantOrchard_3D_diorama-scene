using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The Wisdom Tree bloom and the maintenance mode after it (CLAUDE.md 5.9). Reaching the final stage sets
    /// GameState.BloomActive and announces BloomStarted once. From then on every fruit in GameConfig hangs on the tree:
    /// a tap picks a ripe one (no gesture needed) and it regrows after BloomFruitRegrowSeconds
    /// (GameState.bloomFruitLastHarvestUtc). A picked fruit goes the usual way as FruitHarvested with Plot = Tree, so
    /// delivery, the basket and healing need nothing new; never golden. A save already at the final stage blooms
    /// silently at launch. Dispose to unsubscribe.
    /// </summary>
    public sealed class WisdomTreeBloom : IDisposable
    {
        /// <summary>FruitHarvested.Plot (and HarvestHeld.Plot) for fruit picked from the blooming Wisdom Tree.</summary>
        public const int Tree = -2;

        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly TimeService _time;
        private readonly EventBus _events;
        private readonly Action _markDirty;
        // Ripe or not as last announced, per GameConfig.Fruits index; Tick announces the changes.
        private readonly bool[] _ripe;

        public WisdomTreeBloom(GameState state, GameConfig config, TimeService time, EventBus events, Action markDirty)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _ripe = new bool[_config.Fruits.Count];

            CatchUp();
            ReadRipe();
            _events.Subscribe<StageReached>(OnStageReached);
        }

        public void Dispose() => _events.Unsubscribe<StageReached>(OnStageReached);

        public bool IsActive => _state.BloomActive;

        /// <summary>
        /// Asked before a tap picks a fruit: false keeps it on the tree (DeliveryService.CanTake: nobody wants it and its
        /// basket is full), as for the plots. Null lets every pick through.
        /// </summary>
        public Func<string, bool> HarvestGate { get; set; }

        private int RegrowSeconds => _config.Balance.BloomFruitRegrowSeconds;

        public bool IsRipe(string fruitId) =>
            IsActive && IndexOf(fruitId) >= 0 && BloomRules.IsRipe(_state.GetBloomFruitLastHarvest(fruitId), _time.NowUtcMs, RegrowSeconds);

        public int SecondsUntilRipe(string fruitId) =>
            BloomRules.SecondsUntilRipe(_state.GetBloomFruitLastHarvest(fruitId), _time.NowUtcMs, RegrowSeconds);

        /// <summary>
        /// A tap on this fruit on the tree: picks it when ripe (Harvest), keeps it on the tree when it has nowhere to go
        /// (Held, with HarvestHeld), otherwise does nothing (Ignore).
        /// </summary>
        public HarvestJudgement TryHarvest(string fruitId)
        {
            int index = IndexOf(fruitId);
            if (!IsRipe(fruitId)) return HarvestJudgement.Ignore;
            if (HarvestGate != null && !HarvestGate(fruitId))
            {
                _events.Publish(new HarvestHeld(Tree, fruitId));
                return HarvestJudgement.Held;
            }

            _state.SetBloomFruitLastHarvest(fruitId, _time.NowUtcMs);
            _markDirty();
            _ripe[index] = false;
            _events.Publish(new BloomFruitChanged(fruitId, false));
            _events.Publish(new FruitHarvested(Tree, fruitId));
            return HarvestJudgement.Harvest;
        }

        /// <summary>Dev tool: every fruit on the blooming tree ripe now. Returns how many grew back.</summary>
        public int RipenAll()
        {
            if (!IsActive) return 0;
            int count = 0;
            for (int i = 0; i < _config.Fruits.Count; i++)
            {
                string id = _config.Fruits[i].Id;
                if (IsRipe(id)) continue;
                _state.SetBloomFruitLastHarvest(id, 0);
                count++;
            }
            if (count > 0) _markDirty();
            Tick();
            return count;
        }

        /// <summary>On the GameClock: announces fruits that have grown back.</summary>
        public void Tick()
        {
            if (!IsActive) return;
            for (int i = 0; i < _ripe.Length; i++)
            {
                string id = _config.Fruits[i].Id;
                bool ripe = BloomRules.IsRipe(_state.GetBloomFruitLastHarvest(id), _time.NowUtcMs, RegrowSeconds);
                if (ripe == _ripe[i]) continue;
                _ripe[i] = ripe;
                _events.Publish(new BloomFruitChanged(id, ripe));
            }
        }

        private void OnStageReached(StageReached e)
        {
            if (!e.IsFinal || IsActive) return;
            _state.BloomActive = true;
            _markDirty();
            ReadRipe();
            _events.Publish(new BloomStarted());
            Telemetry.Log("bloom_started");
        }

        // A save at the final stage without the bloom (an older build, or a crash before the save): no celebration at launch.
        private void CatchUp()
        {
            if (IsActive || _state.VibrancyStage < _config.Balance.StageThresholds.Count) return;
            Log.Info("WisdomTreeBloom: final stage reached before this launch; the tree blooms without the celebration.");
            _state.BloomActive = true;
            _markDirty();
        }

        private void ReadRipe()
        {
            for (int i = 0; i < _ripe.Length; i++) _ripe[i] = IsRipe(_config.Fruits[i].Id);
        }

        private int IndexOf(string fruitId)
        {
            for (int i = 0; i < _config.Fruits.Count; i++)
                if (_config.Fruits[i] != null && _config.Fruits[i].Id == fruitId) return i;
            return -1;
        }
    }
}
