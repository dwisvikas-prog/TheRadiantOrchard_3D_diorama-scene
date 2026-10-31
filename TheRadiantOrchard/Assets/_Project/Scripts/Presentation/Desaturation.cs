using System.Collections;
using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Drives the "Radiant Orchard/Sprite Desaturate" shader on a set of renderers (sprites or tilemaps) that share the
    /// M_SpriteDesaturate material (ENGINEERING_GUIDE 8): grey ↔ colour, a dimmed grey, and a colour sweep that fills
    /// them along a world direction. Values go through one reused MaterialPropertyBlock, never a material copy.
    /// Allocation-free after Awake.
    /// </summary>
    public sealed class Desaturation : MonoBehaviour
    {
        public static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        public static readonly int DarkenId = Shader.PropertyToID("_Darken");
        public static readonly int SweepOriginId = Shader.PropertyToID("_SweepOrigin");
        public static readonly int SweepDirectionId = Shader.PropertyToID("_SweepDirection");
        public static readonly int SweepHeightId = Shader.PropertyToID("_SweepHeight");
        public static readonly int SweepSoftnessId = Shader.PropertyToID("_SweepSoftness");

        [SerializeField] private Renderer[] _renderers = new Renderer[0];
        [Range(0f, 1f)] [SerializeField] private float _saturation = 1f;
        [Range(0f, 1f)] [SerializeField] private float _darken;
        [Tooltip("Soft edge of the colour sweep, in world units.")]
        [Min(0f)] [SerializeField] private float _sweepSoftness = 0.15f;

        private MaterialPropertyBlock _block;
        private Coroutine _sweep;
        private Vector2 _sweepOrigin;
        // When set, the sweep starts from this transform each frame (a visitor's feet while they hop and walk off).
        private Transform _sweepAnchor;
        private Vector2 _sweepDirection = Vector2.up;
        private float _sweepHeight = -1f;

        public float Saturation => _saturation;
        public bool IsSweeping => _sweep != null;

        /// <summary>Sets the renderers for an object built in code (prefabs and scenes assign them in the builders).</summary>
        public void Bind(params Renderer[] renderers)
        {
            _renderers = renderers ?? new Renderer[0];
            Apply();
        }

        /// <summary>0 = grey, 1 = full colour. Stops any sweep.</summary>
        public void SetSaturation(float saturation)
        {
            StopSweep();
            _saturation = Mathf.Clamp01(saturation);
            _sweepHeight = -1f;
            Apply();
        }

        /// <summary>How much darker the grey part is (0..1).</summary>
        public void SetDarken(float darken)
        {
            _darken = Mathf.Clamp01(darken);
            Apply();
        }

        /// <summary>
        /// Colour washes from origin along direction over length world units in seconds, then everything is in full
        /// colour (saturation 1).
        /// </summary>
        public void Sweep(Vector2 origin, Vector2 direction, float length, float seconds)
        {
            StopSweep();
            _sweepAnchor = null;
            _sweepOrigin = origin;
            _sweepDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
            if (!isActiveAndEnabled || seconds <= 0f)
            {
                SetSaturation(1f);
                return;
            }
            _sweep = StartCoroutine(SweepRoutine(length, seconds));
        }

        /// <summary>As Sweep, starting from the anchor's position every frame, so the colour moves with it.</summary>
        public void Sweep(Transform anchor, Vector2 direction, float length, float seconds)
        {
            Sweep(anchor.position, direction, length, seconds);
            if (_sweep != null) _sweepAnchor = anchor;
        }

        /// <summary>Sets the sweep front directly (tests and scripted sequences): length world units from origin.</summary>
        public void SetSweep(Vector2 origin, Vector2 direction, float height)
        {
            StopSweep();
            _sweepOrigin = origin;
            _sweepDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.up;
            _sweepHeight = height;
            Apply();
        }

        private IEnumerator SweepRoutine(float length, float seconds)
        {
            // Start just behind the origin so nothing is coloured on the first frame.
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _sweepHeight = Mathf.Lerp(-_sweepSoftness, length + _sweepSoftness, t / seconds);
                if (_sweepAnchor != null) _sweepOrigin = _sweepAnchor.position;
                Apply();
                yield return null;
            }
            _sweep = null;
            _sweepAnchor = null;
            _saturation = 1f;
            _sweepHeight = -1f;
            Apply();
        }

        private void StopSweep()
        {
            if (_sweep != null) StopCoroutine(_sweep);
            _sweep = null;
        }

        private void Apply()
        {
            _block ??= new MaterialPropertyBlock();
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_block);
                _block.SetFloat(SaturationId, _saturation);
                _block.SetFloat(DarkenId, _darken);
                _block.SetVector(SweepOriginId, _sweepOrigin);
                _block.SetVector(SweepDirectionId, _sweepDirection);
                _block.SetFloat(SweepHeightId, _sweepHeight);
                _block.SetFloat(SweepSoftnessId, _sweepSoftness);
                r.SetPropertyBlock(_block);
            }
        }

        private void OnDisable() => _sweep = null;

        private void Awake()
        {
            if (_renderers.Length == 0)
                Log.Warn($"Desaturation '{name}': no renderers assigned; nothing will change colour.", this);
            Apply();
        }

#if UNITY_EDITOR
        // Also in edit mode, so the Scene view shows the same grey as Play.
        private void OnValidate() => Apply();
#endif
    }
}
