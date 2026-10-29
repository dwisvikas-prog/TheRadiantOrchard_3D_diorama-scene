using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Every global tuning number from CLAUDE.md section 5 (per-fruit numbers live on FruitDefinition).
    /// All values are starting points for the week-long pacing pass; change them here, never in code.
    /// Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        public const int StageCount = 5;

        [Header("Start and plots (5.3)")]
        [Min(0)] [SerializeField] private int _startingSeeds = 0;
        [Tooltip("Plots open at each vibrancy stage, index 0 = start ... index 5 = stage 5.")]
        [SerializeField] private int[] _plotsOpenAtStage = { 3, 3, 5, 7, 9, 9 };
        [Tooltip("A newly planted tree shows as a sapling for this fraction of its grow time, then as growing.")]
        [Range(0.05f, 0.9f)] [SerializeField] private float _saplingFraction = 0.25f;

        [Header("Visitors (5.4)")]
        [Min(1f)] [SerializeField] private float _visitorSpawnIntervalSeconds = 30f;
        [Min(1)] [SerializeField] private int _maxVisitorsOnPath = 3;
        [Min(1f)] [SerializeField] private float _patienceSeconds = 90f;
        [Min(1)] [SerializeField] private int _benchSeats = 3;
        [Tooltip("Visitors already waiting on the path after time away.")]
        [Min(0)] [SerializeField] private int _maxOfflineArrivals = 3;

        [Header("Delivery (5.5)")]
        [Min(1)] [SerializeField] private int _basketMaxPerFruit = 3;

        [Header("Healing rewards (5.6)")]
        [Min(0)] [SerializeField] private int _seedsPerHeal = 10;
        [Min(0)] [SerializeField] private int _seedsPerBenchHeal = 5;

        [Header("Vibrancy stages (5.7)")]
        [Tooltip("Total visitors healed to reach stage 1..5.")]
        [SerializeField] private int[] _stageThresholds = { 40, 120, 250, 450, 700 };

        [Header("Wisdom Tree quiz and Golden Harvest (5.8)")]
        [Min(1)] [SerializeField] private int _quizEveryHeals = 25;
        [Min(1f)] [SerializeField] private float _goldenHarvestSeconds = 60f;
        [Tooltip("Seed multiplier during Golden Harvest (10 x 2 = 20 per heal).")]
        [Min(1)] [SerializeField] private int _goldenHarvestSeedMultiplier = 2;

        [Header("Wisdom Tree bloom (5.9)")]
        [Min(1)] [SerializeField] private int _bloomFruitRegrowSeconds = 3600;

        [Header("Daily visitor wave (5.10)")]
        [Min(1)] [SerializeField] private int _dailyWaveVisitors = 3;
        [Min(1f)] [SerializeField] private float _dailyWavePatienceSeconds = 120f;
        [Min(0)] [SerializeField] private int _dailyWaveBonusSeeds = 100;

        [Header("Mini-events (5.11)")]
        [Min(1)] [SerializeField] private int _miniEventEveryHeals = 75;

        [Header("Golden fruit (5.12)")]
        [Range(0f, 1f)] [SerializeField] private float _goldenFruitChance = 0.05f;
        [Min(1)] [SerializeField] private int _goldenFruitSeedMultiplier = 3;

        [Header("Combo (5.13)")]
        [Tooltip("A heal within this many seconds of the previous one grows the combo.")]
        [Min(0.1f)] [SerializeField] private float _comboWindowSeconds = 4f;

        [Header("Stone Well (5.14)")]
        [Min(1)] [SerializeField] private int _wellTextMaxLength = 60;

        [Header("Input (5.5); remaining gesture thresholds are added and tuned on device in W1-19")]
        [Range(0.2f, 1.5f)] [SerializeField] private float _longPressSeconds = 0.55f;
        [Tooltip("Longest press (seconds) that still counts as a tap. Must stay below the long press.")]
        [Range(0.1f, 0.5f)] [SerializeField] private float _tapMaxSeconds = 0.3f;

        [Header("Camera (5.1)")]
        [Tooltip("Orthographic size at the default zoom. Art is drawn at 2x, so 10.8 shows it at half size on a 1080p screen.")]
        [Min(1f)] [SerializeField] private float _cameraDefaultOrthoSize = 10.8f;
        [Tooltip("Closest zoom. 5.4 shows the 2x art at full resolution on a 1080p screen.")]
        [Min(1f)] [SerializeField] private float _cameraMinOrthoSize = 5.4f;
        [Tooltip("Farthest zoom (also limited by the farm bounds).")]
        [Min(1f)] [SerializeField] private float _cameraMaxOrthoSize = 13.5f;
        [Tooltip("Zoom change per mouse-wheel notch (Editor), as a fraction of the current size.")]
        [Range(0.01f, 0.5f)] [SerializeField] private float _cameraScrollZoomFactor = 0.1f;
        [Tooltip("How far a finger must move, as a fraction of screen height, before a drag pans the camera. Shorter moves stay taps/gestures.")]
        [Range(0.001f, 0.1f)] [SerializeField] private float _cameraDragThreshold = 0.01f;

        [Header("Saving (ENGINEERING_GUIDE 5.5)")]
        [Tooltip("Minimum real seconds between autosaves while there are unsaved changes. Pause and quit always save.")]
        [Min(0.5f)] [SerializeField] private float _autosaveMinIntervalSeconds = 3f;

        [Header("Performance (ENGINEERING_GUIDE 2, 6)")]
        [Range(30, 120)] [SerializeField] private int _targetFrameRate = 60;
        [Tooltip("Seconds between simulation ticks (growth, patience). 0.25 = 4 per second.")]
        [Range(0.05f, 1f)] [SerializeField] private float _gameTickIntervalSeconds = 0.25f;

        public int StartingSeeds => _startingSeeds;
        public IReadOnlyList<int> PlotsOpenAtStage => _plotsOpenAtStage;
        public int TotalPlots => _plotsOpenAtStage[_plotsOpenAtStage.Length - 1];

        public float VisitorSpawnIntervalSeconds => _visitorSpawnIntervalSeconds;
        public int MaxVisitorsOnPath => _maxVisitorsOnPath;
        public float PatienceSeconds => _patienceSeconds;
        public int BenchSeats => _benchSeats;
        public int MaxOfflineArrivals => _maxOfflineArrivals;

        public int BasketMaxPerFruit => _basketMaxPerFruit;

        public int SeedsPerHeal => _seedsPerHeal;
        public int SeedsPerBenchHeal => _seedsPerBenchHeal;

        public IReadOnlyList<int> StageThresholds => _stageThresholds;

        public int QuizEveryHeals => _quizEveryHeals;
        public float GoldenHarvestSeconds => _goldenHarvestSeconds;
        public int GoldenHarvestSeedMultiplier => _goldenHarvestSeedMultiplier;

        public int BloomFruitRegrowSeconds => _bloomFruitRegrowSeconds;

        public int DailyWaveVisitors => _dailyWaveVisitors;
        public float DailyWavePatienceSeconds => _dailyWavePatienceSeconds;
        public int DailyWaveBonusSeeds => _dailyWaveBonusSeeds;

        public int MiniEventEveryHeals => _miniEventEveryHeals;

        public float GoldenFruitChance => _goldenFruitChance;
        public int GoldenFruitSeedMultiplier => _goldenFruitSeedMultiplier;

        public float ComboWindowSeconds => _comboWindowSeconds;

        public int WellTextMaxLength => _wellTextMaxLength;

        public float LongPressSeconds => _longPressSeconds;
        public float TapMaxSeconds => _tapMaxSeconds;

        public float CameraDefaultOrthoSize => _cameraDefaultOrthoSize;
        public float CameraMinOrthoSize => _cameraMinOrthoSize;
        public float CameraMaxOrthoSize => _cameraMaxOrthoSize;
        public float CameraScrollZoomFactor => _cameraScrollZoomFactor;
        public float CameraDragThreshold => _cameraDragThreshold;

        public float AutosaveMinIntervalSeconds => _autosaveMinIntervalSeconds;

        public int TargetFrameRate => _targetFrameRate;
        public float GameTickIntervalSeconds => _gameTickIntervalSeconds;
        public float SaplingFraction => _saplingFraction;

        /// <summary>Number of open plots at a vibrancy stage (0 = start, 5 = farm complete).</summary>
        public int PlotsOpenAt(int stage) => _plotsOpenAtStage[Mathf.Clamp(stage, 0, StageCount)];

        /// <summary>Total heals needed to reach a stage (1..5).</summary>
        public int HealsToReach(int stage) => _stageThresholds[Mathf.Clamp(stage, 1, StageCount) - 1];

        private void OnValidate()
        {
            if (_plotsOpenAtStage == null || _plotsOpenAtStage.Length != StageCount + 1)
                Debug.LogError($"{name}: Plots Open At Stage needs {StageCount + 1} entries (start + stages 1-5).", this);
            else
                for (int i = 1; i < _plotsOpenAtStage.Length; i++)
                    if (_plotsOpenAtStage[i] < _plotsOpenAtStage[i - 1])
                        Debug.LogError($"{name}: Plots Open At Stage must never go down (index {i}).", this);

            if (_stageThresholds == null || _stageThresholds.Length != StageCount)
                Debug.LogError($"{name}: Stage Thresholds needs {StageCount} entries.", this);
            else
                for (int i = 1; i < _stageThresholds.Length; i++)
                    if (_stageThresholds[i] <= _stageThresholds[i - 1])
                        Debug.LogError($"{name}: Stage Thresholds must increase (stage {i + 1}).", this);

            if (_seedsPerBenchHeal > _seedsPerHeal)
                Debug.LogWarning($"{name}: bench heals pay more than path heals; the spec says half.", this);

            if (!(_cameraMinOrthoSize <= _cameraDefaultOrthoSize && _cameraDefaultOrthoSize <= _cameraMaxOrthoSize))
                Debug.LogError($"{name}: camera sizes must be Min <= Default <= Max.", this);
        }
    }
}
