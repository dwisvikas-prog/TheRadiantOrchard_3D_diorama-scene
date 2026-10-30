using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// One pooled tonic: a glowing sprite tinted with the fruit's colour and a short trail (CLAUDE.md 5.5).
    /// Moved by TonicFlights; it has no Update of its own. Tinting uses the renderer colours, so tonics keep batching
    /// and no material is ever created at runtime.
    /// </summary>
    public sealed class TonicView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _sprite;
        [SerializeField] private TrailRenderer _trail;

        private Vector3 _baseScale;
        private Color _colour;

        public void Launch(Vector2 from, Color colour)
        {
            _colour = colour;
            transform.position = new Vector3(from.x, from.y, 0f);
            transform.localScale = _baseScale;
            _sprite.color = colour;
            _trail.startColor = colour;
            _trail.endColor = new Color(colour.r, colour.g, colour.b, 0f);
            // A reused tonic would otherwise draw a streak from where its last flight ended.
            _trail.Clear();
            _trail.emitting = true;
        }

        public void MoveTo(Vector2 position) => transform.position = new Vector3(position.x, position.y, 0f);

        /// <summary>Arrival pop: grows and fades out over k = 0..1.</summary>
        public void Pop(float k, float maxScale)
        {
            _trail.emitting = false;
            transform.localScale = _baseScale * Mathf.Lerp(1f, maxScale, k);
            _sprite.color = new Color(_colour.r, _colour.g, _colour.b, _colour.a * (1f - k));
        }

        private void Awake()
        {
            _baseScale = transform.localScale;
            if (_sprite == null || _trail == null)
                Log.Error($"TonicView '{name}': Sprite and Trail must be assigned; rebuild the tonic (Radiant Orchard > Setup > Build Tonic).", this);
        }
    }
}
