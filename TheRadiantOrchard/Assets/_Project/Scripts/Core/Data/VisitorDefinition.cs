using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// One kind of visitor. Phase 1 has a single grey stick figure; the fields are the seam for Phase 2's special
    /// visitors (two-fruit requests, bigger rewards; ENGINEERING_GUIDE 13). Read-only at runtime.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Visitor Definition", fileName = "Visitor_")]
    public sealed class VisitorDefinition : ScriptableObject
    {
        [SerializeField] private string _id = "default";
        [Tooltip("Fruits asked for at once. Phase 1 visitors ask for one.")]
        [Min(1)] [SerializeField] private int _requestCount = 1;
        [Tooltip("Multiplies the seeds a heal pays. 1 for the normal visitor.")]
        [Min(0f)] [SerializeField] private float _rewardMultiplier = 1f;

        public string Id => _id;
        public int RequestCount => _requestCount;
        public float RewardMultiplier => _rewardMultiplier;
    }
}
