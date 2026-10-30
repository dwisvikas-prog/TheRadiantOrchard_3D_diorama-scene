using System.Collections.Generic;
using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows virtue pop-ups from a pre-warmed pool and moves every live one from a single Step (ENGINEERING_GUIDE 7),
    /// like TonicFlights.
    /// </summary>
    public sealed class VirtuePopups : MonoBehaviour
    {
        private struct Live
        {
            public VirtuePopup Popup;
            public float Elapsed;
        }

        [SerializeField] private VirtuePopup _prefab;
        [Header("Feel (visual tuning)")]
        [Min(0.1f)] [SerializeField] private float _seconds = 1.4f;
        [SerializeField] private float _riseDistance = 1.2f;
        [Min(1)] [SerializeField] private int _prewarm = 2;

        private readonly List<Live> _live = new List<Live>(8);
        private ObjectPool<VirtuePopup> _pool;

        public int ActiveCount => _live.Count;

        public void Show(string text, Color colour, Vector2 position)
        {
            EnsurePool();
            VirtuePopup popup = _pool.Get();
            popup.Show(text, colour, position);
            _live.Add(new Live { Popup = popup });
        }

        public void Step(float dt)
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                Live live = _live[i];
                live.Elapsed += dt;
                if (live.Elapsed < _seconds)
                {
                    live.Popup.Set(live.Elapsed / _seconds, _riseDistance);
                    _live[i] = live;
                    continue;
                }

                _pool.Release(live.Popup);
                int last = _live.Count - 1;
                _live[i] = _live[last];
                _live.RemoveAt(last);
            }
        }

        private void Update() => Step(Time.deltaTime);

        private void Start() => EnsurePool();

        private void EnsurePool()
        {
            if (_pool != null) return;
            _pool = new ObjectPool<VirtuePopup>(
                () => Instantiate(_prefab, transform),
                popup => popup.gameObject.SetActive(true),
                popup => popup.gameObject.SetActive(false),
                popup => Destroy(popup.gameObject),
                collectionCheck: false);

            var warm = new VirtuePopup[_prewarm];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        }

        private void Awake()
        {
            if (_prefab == null)
                Log.Error($"VirtuePopups '{name}': the pop-up prefab must be assigned; rebuild it (Radiant Orchard > Setup > Build Placeholder Visitor).", this);
        }
    }
}
