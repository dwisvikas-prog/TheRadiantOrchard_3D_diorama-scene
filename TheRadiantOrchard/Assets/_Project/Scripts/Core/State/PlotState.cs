using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>One plot in the save file. Timestamps are Unix milliseconds UTC; 0 means "never".</summary>
    [Serializable]
    public sealed class PlotState
    {
        [SerializeField] private bool _unlocked;
        [Tooltip("Planted fruit id, empty if nothing is planted. Trees are permanent once planted.")]
        [SerializeField] private string _fruitId = string.Empty;
        [SerializeField] private long _plantedUtc;
        [SerializeField] private long _lastHarvestUtc;

        public bool Unlocked { get => _unlocked; set => _unlocked = value; }
        public string FruitId { get => _fruitId; set => _fruitId = value ?? string.Empty; }
        public long PlantedUtc { get => _plantedUtc; set => _plantedUtc = value; }
        public long LastHarvestUtc { get => _lastHarvestUtc; set => _lastHarvestUtc = value; }

        public bool IsPlanted => !string.IsNullOrEmpty(_fruitId);
    }
}
