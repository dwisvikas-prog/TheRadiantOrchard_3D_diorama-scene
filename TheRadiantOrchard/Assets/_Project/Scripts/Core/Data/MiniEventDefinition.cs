using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// One mini-event (CLAUDE.md 5.11): its kind, the banner text keys, how often it is picked and its numbers. Only the
    /// fields its kind uses matter (duration and multiplier for Golden Hour, visitor count for Visitor Rush). Data only;
    /// read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Mini Event", fileName = "MiniEvent_")]
    public sealed class MiniEventDefinition : ScriptableObject
    {
        [Tooltip("Stable lowercase id, e.g. 'golden_hour' (telemetry, dev tools).")]
        [SerializeField] private string _id;
        [SerializeField] private MiniEventKind _kind;
        [SerializeField] private string _titleKey;
        [Tooltip("Banner line; {0} is the duration as m:ss.")]
        [SerializeField] private string _bodyKey;
        [Tooltip("Relative chance of being picked.")]
        [Min(0f)] [SerializeField] private float _weight = 1f;
        [Min(0f)] [SerializeField] private float _durationSeconds;
        [Min(1f)] [SerializeField] private float _multiplier = 1f;
        [Min(0)] [SerializeField] private int _visitorCount;

        public string Id => _id;
        public MiniEventKind Kind => _kind;
        public string TitleKey => _titleKey;
        public string BodyKey => _bodyKey;
        public float Weight => _weight;
        public float DurationSeconds => _durationSeconds;
        public float Multiplier => _multiplier;
        public int VisitorCount => _visitorCount;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(_id)) Debug.LogWarning($"MiniEventDefinition '{name}' has no id.", this);
        }
    }
}
