using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// All player progress: the save file (CLAUDE.md section 7, schema v1). The single source of truth; nothing else
    /// stores progress. Plain data: systems change it, call SaveService.MarkDirty() and raise their own EventBus
    /// events. Timestamps are Unix milliseconds UTC (0 = never) because JsonUtility cannot serialize DateTime.
    /// </summary>
    [Serializable]
    public sealed class GameState
    {
        public const int CurrentVersion = 1;

        [SerializeField] private int _version = CurrentVersion;
        [SerializeField] private long _lastSaveUtc;

        [SerializeField] private int _seeds;
        [SerializeField] private int _healedTotal;
        [SerializeField] private int _healedSinceQuiz;
        [SerializeField] private int _healedSinceEvent;
        [SerializeField] private int _vibrancyStage;

        [SerializeField] private List<PlotState> _plots = new List<PlotState>();
        [SerializeField] private List<FruitCount> _basket = new List<FruitCount>();
        [SerializeField] private List<BenchVisitorState> _bench = new List<BenchVisitorState>();

        [SerializeField] private List<string> _quizSeen = new List<string>();
        [SerializeField] private bool _bloomActive;
        [SerializeField] private List<FruitTimestamp> _bloomFruitLastHarvestUtc = new List<FruitTimestamp>();

        [SerializeField] private string _wellText = string.Empty;
        [SerializeField] private bool _tutorialDone;
        [Tooltip("Local date of the last daily visitor wave, yyyy-MM-dd; empty if never.")]
        [SerializeField] private string _lastDailyWaveDate = string.Empty;
        [SerializeField] private SettingsState _settings = new SettingsState();

        public int Version { get => _version; set => _version = value; }
        public long LastSaveUtc { get => _lastSaveUtc; set => _lastSaveUtc = value; }

        /// <summary>Seeds held. Never negative; spending more than you have is the caller's bug, so it clamps.</summary>
        public int Seeds { get => _seeds; set => _seeds = Math.Max(0, value); }
        public int HealedTotal { get => _healedTotal; set => _healedTotal = value; }
        public int HealedSinceQuiz { get => _healedSinceQuiz; set => _healedSinceQuiz = value; }
        public int HealedSinceEvent { get => _healedSinceEvent; set => _healedSinceEvent = value; }
        public int VibrancyStage { get => _vibrancyStage; set => _vibrancyStage = value; }

        public IReadOnlyList<PlotState> Plots => _plots;
        public List<BenchVisitorState> Bench => _bench;
        public List<string> QuizSeen => _quizSeen;
        public bool BloomActive { get => _bloomActive; set => _bloomActive = value; }

        public string WellText { get => _wellText; set => _wellText = value ?? string.Empty; }
        public bool TutorialDone { get => _tutorialDone; set => _tutorialDone = value; }
        public string LastDailyWaveDate { get => _lastDailyWaveDate; set => _lastDailyWaveDate = value ?? string.Empty; }
        public SettingsState Settings => _settings;

        /// <summary>A first-launch state: starting seeds, all plots present, only the starting plots open.</summary>
        public static GameState CreateNew(BalanceConfig balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            var state = new GameState { Seeds = balance.StartingSeeds };
            int openAtStart = balance.PlotsOpenAt(0);
            for (int i = 0; i < balance.TotalPlots; i++)
                state._plots.Add(new PlotState { Unlocked = i < openAtStart });
            return state;
        }

        /// <summary>
        /// Repairs a loaded state so a partly broken or older file never crashes the game: missing lists and settings
        /// are created, text fields are never null, and missing plots are added (locked).
        /// </summary>
        public void EnsureIntegrity(BalanceConfig balance)
        {
            if (balance == null) throw new ArgumentNullException(nameof(balance));

            _plots ??= new List<PlotState>();
            _basket ??= new List<FruitCount>();
            _bench ??= new List<BenchVisitorState>();
            _quizSeen ??= new List<string>();
            _bloomFruitLastHarvestUtc ??= new List<FruitTimestamp>();
            _settings ??= new SettingsState();
            _wellText ??= string.Empty;
            _lastDailyWaveDate ??= string.Empty;
            _seeds = Math.Max(0, _seeds);

            for (int i = 0; i < _plots.Count; i++)
                _plots[i] ??= new PlotState();
            while (_plots.Count < balance.TotalPlots)
                _plots.Add(new PlotState());
        }

        public int GetBasketCount(string fruitId)
        {
            int i = IndexOf(_basket, fruitId);
            return i >= 0 ? _basket[i].Count : 0;
        }

        /// <summary>How many of the basket's fruit of this kind are golden.</summary>
        public int GetBasketGoldenCount(string fruitId)
        {
            int i = IndexOf(_basket, fruitId);
            return i >= 0 ? Math.Min(_basket[i].Golden, _basket[i].Count) : 0;
        }

        /// <summary>
        /// Sets the basket count for a fruit and how many of them are golden (clamped to the count); a count of 0 or less
        /// removes the entry.
        /// </summary>
        public void SetBasketCount(string fruitId, int count, int golden = 0)
        {
            int i = IndexOf(_basket, fruitId);
            if (count <= 0)
            {
                if (i >= 0) _basket.RemoveAt(i);
                return;
            }
            if (i < 0)
            {
                _basket.Add(new FruitCount(fruitId, count));
                i = _basket.Count - 1;
            }
            _basket[i].Count = count;
            _basket[i].Golden = Math.Max(0, Math.Min(golden, count));
        }

        /// <summary>When a bloom fruit was last harvested (Unix ms UTC), or 0 if never.</summary>
        public long GetBloomFruitLastHarvest(string fruitId)
        {
            for (int i = 0; i < _bloomFruitLastHarvestUtc.Count; i++)
                if (_bloomFruitLastHarvestUtc[i].FruitId == fruitId)
                    return _bloomFruitLastHarvestUtc[i].Utc;
            return 0;
        }

        public void SetBloomFruitLastHarvest(string fruitId, long utc)
        {
            for (int i = 0; i < _bloomFruitLastHarvestUtc.Count; i++)
            {
                if (_bloomFruitLastHarvestUtc[i].FruitId == fruitId)
                {
                    _bloomFruitLastHarvestUtc[i].Utc = utc;
                    return;
                }
            }
            _bloomFruitLastHarvestUtc.Add(new FruitTimestamp(fruitId, utc));
        }

        private static int IndexOf(List<FruitCount> list, string fruitId)
        {
            for (int i = 0; i < list.Count; i++)
                if (list[i].FruitId == fruitId)
                    return i;
            return -1;
        }
    }
}
