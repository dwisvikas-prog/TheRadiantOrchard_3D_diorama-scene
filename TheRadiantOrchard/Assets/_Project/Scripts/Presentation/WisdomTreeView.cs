using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// The Wisdom Tree landmark: the soft gold glow that says a quiz is waiting (CLAUDE.md 5.8), and the area a tap
    /// must hit to open it. Update only runs the pulse while glowing.
    /// </summary>
    public sealed class WisdomTreeView : MonoBehaviour
    {
        [Tooltip("The tree's body; its bounds are the tap area.")]
        [SerializeField] private SpriteRenderer _body;
        [SerializeField] private SpriteRenderer _glow;
        [SerializeField] private float _pulseSeconds = 1.6f;
        [Range(0f, 1f)] [SerializeField] private float _minAlpha = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float _maxAlpha = 0.85f;
        [SerializeField] private float _pulseScale = 1.08f;

        private Vector3 _glowScale;
        private bool _cached;
        private float _time;

        public bool IsGlowing => _glow != null && _glow.enabled;

        public void SetGlowing(bool glowing)
        {
            if (_glow == null) return;
            Cache();
            _glow.enabled = glowing;
            _time = 0f;
            Pulse();
        }

        /// <summary>Whether a world point is on the tree (its body bounds, a little taller to include the glow).</summary>
        public bool Contains(Vector2 world)
        {
            if (_body == null) return false;
            Bounds b = _body.bounds;
            b.Expand(new Vector3(0.4f, 0.4f, 0f));
            return b.Contains(new Vector3(world.x, world.y, b.center.z));
        }

        private void Update()
        {
            if (!IsGlowing) return;
            _time += Time.deltaTime;
            Pulse();
        }

        private void Pulse()
        {
            float k = 0.5f - 0.5f * Mathf.Cos(_time / Mathf.Max(0.01f, _pulseSeconds) * Mathf.PI * 2f);
            Color c = _glow.color;
            c.a = Mathf.Lerp(_minAlpha, _maxAlpha, k);
            _glow.color = c;
            _glow.transform.localScale = _glowScale * Mathf.Lerp(1f, _pulseScale, k);
        }

        // The GameInstaller may light the tree before this object's Awake runs, so the glow's built size is read on
        // first use, before any pulse has changed it. The glow is built switched off.
        private void Cache()
        {
            if (_cached) return;
            _glowScale = _glow.transform.localScale;
            _cached = true;
        }

        private void Awake()
        {
            if (_body == null || _glow == null)
            {
                Log.Error($"WisdomTreeView '{name}': Body and Glow must be assigned; rebuild the farm (Radiant Orchard > Setup > Build Placeholder Farm).", this);
                return;
            }
            Cache();
        }
    }
}
