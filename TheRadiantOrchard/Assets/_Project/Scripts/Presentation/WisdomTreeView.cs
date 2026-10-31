using System.Collections.Generic;
using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// The Wisdom Tree landmark: the soft gold glow that says a quiz is waiting (CLAUDE.md 5.8), the bloom with one
    /// fruit per GameConfig fruit (5.9; picked ones fade until they grow back) and the areas a tap must hit. Update only
    /// runs while glowing or while the bloom is opening.
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

        [Header("Bloom (CLAUDE.md 5.9)")]
        [Tooltip("Blossoms and fruits; hidden until the tree blooms.")]
        [SerializeField] private Transform _bloom;
        [Tooltip("The crown behind the fruits; its bounds frame the bloom for the camera and widen the tap area.")]
        [SerializeField] private SpriteRenderer _crown;
        [Tooltip("One per GameConfig fruit, in the same order.")]
        [SerializeField] private SpriteRenderer[] _fruits = new SpriteRenderer[0];
        [Tooltip("How far from a fruit's centre a tap still picks it (world units).")]
        [Min(0.1f)] [SerializeField] private float _fruitTapRadius = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float _pickedAlpha = 0.2f;
        [Min(0.01f)] [SerializeField] private float _revealSeconds = 1.2f;

        private Vector3 _glowScale;
        private Vector3 _bloomScale;
        private bool _cached;
        private float _time;
        private float _reveal = -1f;
        private bool[] _ripe = new bool[0];

        public bool IsGlowing => _glow != null && _glow.enabled;
        public bool IsBlooming => _bloom != null && _bloom.gameObject.activeSelf;
        public bool IsRevealing => _reveal >= 0f;
        public float RevealSeconds => _revealSeconds;
        public int FruitCount => _fruits.Length;

        public void SetGlowing(bool glowing)
        {
            if (_glow == null) return;
            Cache();
            _glow.enabled = glowing;
            _time = 0f;
            Pulse();
        }

        /// <summary>Paints each fruit with its icon (or its tint on the placeholder dot). Same order as GameConfig.Fruits.</summary>
        public void SetFruits(IReadOnlyList<FruitDefinition> fruits)
        {
            if (fruits.Count != _fruits.Length)
                Log.Error($"WisdomTreeView '{name}': {_fruits.Length} fruit spots for {fruits.Count} fruits; rebuild the farm.", this);
            _ripe = new bool[_fruits.Length];
            for (int i = 0; i < _fruits.Length && i < fruits.Count; i++)
            {
                if (_fruits[i] == null || fruits[i] == null) continue;
                if (fruits[i].Icon != null)
                {
                    _fruits[i].sprite = fruits[i].Icon;
                    _fruits[i].color = Color.white;
                }
                else _fruits[i].color = fruits[i].Tint;
                SetFruitRipe(i, true);
            }
        }

        /// <summary>Shows the bloom; animated, it grows out from the tree over RevealSeconds.</summary>
        public void SetBloom(bool blooming, bool animate)
        {
            if (_bloom == null) return;
            Cache();
            _bloom.gameObject.SetActive(blooming);
            _reveal = blooming && animate ? 0f : -1f;
            _bloom.localScale = _reveal >= 0f ? Vector3.zero : _bloomScale;
        }

        public void SetFruitRipe(int index, bool ripe)
        {
            if (index < 0 || index >= _fruits.Length || _fruits[index] == null) return;
            _ripe[index] = ripe;
            Color c = _fruits[index].color;
            c.a = ripe ? 1f : _pickedAlpha;
            _fruits[index].color = c;
        }

        public bool IsFruitRipe(int index) => index >= 0 && index < _ripe.Length && _ripe[index];

        /// <summary>The fruit nearest to a world point within the tap radius while the tree blooms, or -1.</summary>
        public int FruitAt(Vector2 world)
        {
            if (!IsBlooming) return -1;
            int best = -1;
            float bestDistance = _fruitTapRadius * _fruitTapRadius;
            for (int i = 0; i < _fruits.Length; i++)
            {
                if (_fruits[i] == null) continue;
                float d = ((Vector2)_fruits[i].transform.position - world).sqrMagnitude;
                if (d > bestDistance) continue;
                bestDistance = d;
                best = i;
            }
            return best;
        }

        public Vector2 FruitPosition(int index) =>
            index >= 0 && index < _fruits.Length && _fruits[index] != null ? (Vector2)_fruits[index].transform.position : (Vector2)transform.position;

        /// <summary>The whole tree with its crown, in world space, for the camera to frame the bloom.</summary>
        public Rect BloomArea
        {
            get
            {
                Bounds b = _body != null ? _body.bounds : new Bounds(transform.position, Vector3.one);
                if (_crown != null && _crown.sprite != null && _bloom != null)
                {
                    // From the built sizes: the bloom is hidden or scaled down before and during its reveal.
                    Cache();
                    Vector3 bloomScale = Vector3.Scale(_bloomScale, _bloom.parent != null ? _bloom.parent.lossyScale : Vector3.one);
                    Vector3 centre = _bloom.parent != null ? _bloom.parent.TransformPoint(_bloom.localPosition) : _bloom.localPosition;
                    centre += Vector3.Scale(_crown.transform.localPosition, bloomScale);
                    Vector3 size = Vector3.Scale(_crown.sprite.bounds.size, Vector3.Scale(_crown.transform.localScale, bloomScale));
                    b.Encapsulate(new Bounds(centre + Vector3.Scale(_crown.sprite.bounds.center, Vector3.Scale(_crown.transform.localScale, bloomScale)), size));
                }
                return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            }
        }

        /// <summary>Whether a world point is on the tree (its body bounds a little larger, or the crown while blooming).</summary>
        public bool Contains(Vector2 world)
        {
            if (_body == null) return false;
            Bounds b = _body.bounds;
            b.Expand(new Vector3(0.4f, 0.4f, 0f));
            if (IsBlooming && _crown != null) b.Encapsulate(_crown.bounds);
            return b.Contains(new Vector3(world.x, world.y, b.center.z));
        }

        private void Update()
        {
            if (IsGlowing)
            {
                _time += Time.deltaTime;
                Pulse();
            }
            if (_reveal >= 0f)
            {
                _reveal += Time.deltaTime;
                float k = Mathf.Clamp01(_reveal / _revealSeconds);
                // Overshoots a little before settling, so the bloom bursts open.
                float s = Mathf.Sin(k * Mathf.PI * 0.5f) + Mathf.Sin(k * Mathf.PI) * 0.12f;
                _bloom.localScale = _bloomScale * s;
                if (k >= 1f)
                {
                    _bloom.localScale = _bloomScale;
                    _reveal = -1f;
                }
            }
        }

        private void Pulse()
        {
            float k = 0.5f - 0.5f * Mathf.Cos(_time / Mathf.Max(0.01f, _pulseSeconds) * Mathf.PI * 2f);
            Color c = _glow.color;
            c.a = Mathf.Lerp(_minAlpha, _maxAlpha, k);
            _glow.color = c;
            _glow.transform.localScale = _glowScale * Mathf.Lerp(1f, _pulseScale, k);
        }

        // The GameInstaller may light the tree before this object's Awake runs, so the built sizes are read on first
        // use, before any pulse or reveal has changed them. The glow and the bloom are built switched off.
        private void Cache()
        {
            if (_cached) return;
            if (_glow != null) _glowScale = _glow.transform.localScale;
            if (_bloom != null) _bloomScale = _bloom.localScale;
            _cached = true;
        }

        private void Awake()
        {
            if (_body == null || _glow == null || _bloom == null || _crown == null)
            {
                Log.Error($"WisdomTreeView '{name}': Body, Glow, Bloom and Crown must be assigned; rebuild the farm (Radiant Orchard > Setup > Build Placeholder Farm).", this);
                return;
            }
            Cache();
        }
    }
}
