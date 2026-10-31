using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// The daily visitor wave (CLAUDE.md 5.10): on the first launch or return after local midnight, DailyWaveVisitors
    /// visitors walk in together with DailyWavePatienceSeconds of patience; healing all of them pays DailyWaveBonusSeeds.
    /// Agreed for W3·04: the very first launch only records today (day 1 belongs to the tutorial); the wave needs every
    /// path spot free, so regular spawning pauses until they empty; it waits for something to be planted, and the day
    /// is saved only once it arrives; a member healed on the bench still counts; the count is not saved. CheckNewDay
    /// runs at launch, on return from the background and when the dev time offset changes. Dispose to unsubscribe.
    /// </summary>
    public sealed class DailyWave : IDisposable
    {
        private readonly GameState _state;
        private readonly BalanceConfig _balance;
        private readonly TimeService _time;
        private readonly EventBus _events;
        private readonly VisitorManager _visitors;
        private readonly Action _markDirty;
        private readonly int[] _members;
        private readonly bool[] _healed;
        private int _healedCount;

        public DailyWave(GameState state, GameConfig config, TimeService time, EventBus events, VisitorManager visitors, Action markDirty)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _balance = (config ?? throw new ArgumentNullException(nameof(config))).Balance;
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _visitors = visitors ?? throw new ArgumentNullException(nameof(visitors));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _members = new int[Math.Max(1, _balance.DailyWaveVisitors)];
            _healed = new bool[_members.Length];

            if (string.IsNullOrEmpty(_state.LastDailyWaveDate))
            {
                _state.LastDailyWaveDate = _time.LocalDate;
                _markDirty();
            }
            _events.Subscribe<VisitorHealed>(OnHealed);
        }

        public void Dispose() => _events.Unsubscribe<VisitorHealed>(OnHealed);

        /// <summary>A wave is due and waiting for free path spots or something planted.</summary>
        public bool IsPending { get; private set; }
        /// <summary>A wave is on the farm and not all of it has been healed yet.</summary>
        public bool IsActive { get; private set; }
        public int HealedCount => _healedCount;

        /// <summary>A new local day since the last wave (a clock turned back never counts): the wave is due.</summary>
        public void CheckNewDay()
        {
            if (IsPending || !_time.IsNewLocalDay(_state.LastDailyWaveDate)) return;
            Begin();
        }

        /// <summary>Dev tool: a wave now, whatever the date.</summary>
        public void StartNow() => Begin();

        /// <summary>Called on every GameClock tick after the visitors: brings a waiting wave in once it can come.</summary>
        public void Tick()
        {
            if (IsPending) TryArrive();
        }

        private void Begin()
        {
            IsPending = true;
            _visitors.HoldSpawning = true;
            TryArrive();
        }

        private void TryArrive()
        {
            int count = _members.Length;
            if (_visitors.SpawnGroup(count, _balance.DailyWavePatienceSeconds, _members) < count) return;

            IsPending = false;
            _visitors.HoldSpawning = false;
            IsActive = true;
            _healedCount = 0;
            Array.Clear(_healed, 0, _healed.Length);
            _state.LastDailyWaveDate = _time.LocalDate;
            _markDirty();
            _events.Publish(new DailyWaveStarted(count, _balance.DailyWaveBonusSeeds));
            Telemetry.Log("daily_wave_started", _state.LastDailyWaveDate);
        }

        private void OnHealed(VisitorHealed e)
        {
            if (!IsActive) return;
            int i = Array.IndexOf(_members, e.VisitorId);
            if (i < 0 || _healed[i]) return;
            _healed[i] = true;
            if (++_healedCount < _members.Length) return;

            IsActive = false;
            int bonus = _balance.DailyWaveBonusSeeds;
            _state.Seeds += bonus;
            _markDirty();
            _events.Publish(new SeedsChanged(_state.Seeds, bonus));
            _events.Publish(new DailyWaveCompleted(bonus, e.VisitorId));
            Telemetry.Log("daily_wave_completed", bonus.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
