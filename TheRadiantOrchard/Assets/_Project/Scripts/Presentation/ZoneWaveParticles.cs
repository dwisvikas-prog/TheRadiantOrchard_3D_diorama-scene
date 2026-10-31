using RadiantOrchard.Core;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Petals that ride the front of a zone's colour wave (FX-11). One particle system, reused for every zone: Play
    /// stretches its emitter across the zone and Step moves it up with the sweep. Placeholder soft dots until the
    /// petal art exists; particle count is capped by the system's Max Particles (ENGINEERING_GUIDE 7).
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public sealed class ZoneWaveParticles : MonoBehaviour
    {
        [Tooltip("Petals per second while the wave moves.")]
        [Min(0f)] [SerializeField] private float _rate = 45f;
        [Tooltip("Emitter width as a share of the zone's width (iso zones are narrower at the top and bottom).")]
        [Range(0.1f, 1f)] [SerializeField] private float _widthShare = 0.7f;

        private ParticleSystem _system;
        private Rect _area;
        private float _duration;
        private float _elapsed = -1f;

        public bool IsPlaying => _elapsed >= 0f;

        /// <summary>Starts the petals for a wave over this world area lasting seconds.</summary>
        public void Play(Rect area, float seconds)
        {
            Cache();
            _area = area;
            _duration = Mathf.Max(0.01f, seconds);
            _elapsed = 0f;

            ParticleSystem.ShapeModule shape = _system.shape;
            shape.scale = new Vector3(area.width * _widthShare, 0.2f, 1f);
            ParticleSystem.EmissionModule emission = _system.emission;
            emission.rateOverTime = _rate;
            Place(0f);
            _system.Play();
        }

        private void Update()
        {
            if (_elapsed < 0f) return;
            _elapsed += Time.deltaTime;
            float t = _elapsed / _duration;
            if (t >= 1f)
            {
                _elapsed = -1f;
                _system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            Place(t);
        }

        private void Place(float t) =>
            transform.position = new Vector3(_area.center.x, Mathf.Lerp(_area.yMin, _area.yMax, t), transform.position.z);

        private void Cache()
        {
            if (_system == null) _system = GetComponent<ParticleSystem>();
        }

        private void Awake()
        {
            Cache();
            if (_system.main.playOnAwake)
                Log.Warn($"ZoneWaveParticles '{name}': Play On Awake should be off; rebuild with Radiant Orchard > Setup > Build Zone Unlock.", this);
        }
    }
}
