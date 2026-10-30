using System;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// Read-only definition of one fruit tree. Runtime state (what is planted where, timers) lives in GameState,
    /// never here: writing to a ScriptableObject at runtime persists in the Editor and breaks on device.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Fruit Definition", fileName = "Fruit_")]
    public sealed class FruitDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id stored in the save file. Lowercase, never changed after release.")]
        [SerializeField] private string _id;
        [Tooltip("StringTable key for the fruit's display name.")]
        [SerializeField] private string _displayNameKey;
        [Tooltip("The virtue this fruit represents (name and situation lines).")]
        [SerializeField] private VirtueDefinition _virtue;

        [Header("Harvest")]
        [SerializeField] private GestureType _gesture;

        [Header("Growth and economy (starting values for tuning)")]
        [Tooltip("Real-time seconds from planting (or last harvest) until ripe.")]
        [Min(1)] [SerializeField] private int _growSeconds = 300;
        [Tooltip("Seeds paid to plant. 0 = free.")]
        [Min(0)] [SerializeField] private int _plantCost;
        [Tooltip("Vibrancy stage that unlocks this fruit. 0 = available from the start.")]
        [Range(0, 5)] [SerializeField] private int _unlockStage;

        [Header("Visuals")]
        [Tooltip("Fruit colour for the tonic, heal particles and pop-ups.")]
        [SerializeField] private Color _tint = Color.white;
        [SerializeField] private Sprite _icon;
        [SerializeField] private Sprite _saplingSprite;
        [SerializeField] private Sprite _growingSprite;
        [SerializeField] private Sprite _ripeSprite;
        [SerializeField] private GameObject _healVfxPrefab;

        public string Id => _id;
        public string DisplayNameKey => _displayNameKey;
        public VirtueDefinition Virtue => _virtue;
        public string VirtueNameKey => _virtue != null ? _virtue.NameKey : null;
        public GestureType Gesture => _gesture;
        public int GrowSeconds => _growSeconds;
        public TimeSpan GrowTime => TimeSpan.FromSeconds(_growSeconds);
        public int PlantCost => _plantCost;
        public int UnlockStage => _unlockStage;
        public bool IsFree => _plantCost == 0;
        public Color Tint => _tint;
        public Sprite Icon => _icon;
        public Sprite SaplingSprite => _saplingSprite;
        public Sprite GrowingSprite => _growingSprite;
        public Sprite RipeSprite => _ripeSprite;
        public GameObject HealVfxPrefab => _healVfxPrefab;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id))
            {
                Debug.LogWarning($"FruitDefinition '{name}' has no id; it cannot be saved or loaded.", this);
                return;
            }

            if (_virtue == null)
                Debug.LogWarning($"FruitDefinition '{name}' has no virtue; run Radiant Orchard > Setup > Build Virtues.", this);

            string clean = _id.Trim().ToLowerInvariant();
            if (clean != _id)
            {
                Debug.LogWarning($"FruitDefinition '{name}': id '{_id}' should be lowercase without spaces; use '{clean}'.", this);
            }
        }
    }
}
