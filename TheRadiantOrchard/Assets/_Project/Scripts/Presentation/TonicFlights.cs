using System;
using System.Collections.Generic;
using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.Pool;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Flies glowing tonics (CLAUDE.md 5.5): each arcs from the tree to the point it is given and pops. One runner
    /// moves all tonics in flight from a pooled set (ENGINEERING_GUIDE 7), so a flight allocates nothing per frame.
    /// Who a tonic is for is decided elsewhere (HarvestDelivery); the token it carries comes back in Arrived.
    /// </summary>
    public sealed class TonicFlights : MonoBehaviour
    {
        private struct Flight
        {
            public TonicView View;
            public Vector2 Start;
            public Vector2 Control;
            public Vector2 End;
            public float Elapsed;
            public bool HasArrived;
            public string FruitId;
            public int Token;
        }

        [SerializeField] private TonicView _prefab;
        [Header("Feel (visual tuning)")]
        [Min(0.1f)] [SerializeField] private float _flightSeconds = 0.8f;
        [Tooltip("How far above the straight line the arc's halfway point rises (world units).")]
        [SerializeField] private float _arcHeight = 2f;
        [Tooltip("Random sideways nudge across the flight line, up to this much either way (world units).")]
        [Min(0f)] [SerializeField] private float _sidewaysNudge = 0.5f;
        [Min(0.01f)] [SerializeField] private float _popSeconds = 0.15f;
        [Min(1f)] [SerializeField] private float _popScale = 1.6f;
        [Min(1)] [SerializeField] private int _prewarm = 4;

        private readonly List<Flight> _active = new List<Flight>(16);
        private ObjectPool<TonicView> _pool;

        /// <summary>A tonic reached its target: fruit id, the token it was launched with, and the landing point.</summary>
        public event Action<string, int, Vector2> Arrived;

        public int ActiveCount => _active.Count;
        public int PooledCount => _pool != null ? _pool.CountAll : 0;

        /// <summary>Sends one tonic from a point to a point; token comes back in Arrived (e.g. the visitor it is for).</summary>
        public void Launch(Vector2 from, Vector2 to, Color colour, string fruitId, int token)
        {
            EnsurePool();
            TonicView view = _pool.Get();
            view.Launch(from, colour);
            _active.Add(new Flight
            {
                View = view,
                Start = from,
                Control = TonicArc.ControlPoint(from, to, _arcHeight, UnityEngine.Random.Range(-_sidewaysNudge, _sidewaysNudge)),
                End = to,
                FruitId = fruitId,
                Token = token,
            });
        }

        /// <summary>Advances every tonic in flight by dt seconds. Called each frame; allocation-free.</summary>
        public void Step(float dt)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                Flight f = _active[i];
                f.Elapsed += dt;

                if (f.Elapsed < _flightSeconds)
                {
                    f.View.MoveTo(TonicArc.Evaluate(f.Start, f.Control, f.End, f.Elapsed / _flightSeconds));
                    _active[i] = f;
                    continue;
                }

                if (!f.HasArrived)
                {
                    f.HasArrived = true;
                    f.View.MoveTo(f.End);
                    Arrived?.Invoke(f.FruitId, f.Token, f.End);
                }

                float pop = (f.Elapsed - _flightSeconds) / _popSeconds;
                if (pop < 1f)
                {
                    f.View.Pop(pop, _popScale);
                    _active[i] = f;
                    continue;
                }

                // Order does not matter, so remove by swapping with the last flight.
                _pool.Release(f.View);
                int last = _active.Count - 1;
                _active[i] = _active[last];
                _active.RemoveAt(last);
            }
        }

        private void Update() => Step(Time.deltaTime);

        private void Start() => EnsurePool();

        private void EnsurePool()
        {
            if (_pool != null) return;
            _pool = new ObjectPool<TonicView>(
                () => Instantiate(_prefab, transform),
                view => view.gameObject.SetActive(true),
                view => view.gameObject.SetActive(false),
                // Unity clears every pool when Play mode stops, after the scene has already destroyed the tonics.
                view => { if (view != null) Destroy(view.gameObject); },
                collectionCheck: false);

            // Create the first few up front so the first harvests do not instantiate mid-game.
            var warm = new TonicView[_prewarm];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        }

        private void Awake()
        {
            if (_prefab == null)
                Log.Error($"TonicFlights '{name}': the tonic prefab must be assigned; rebuild it (Radiant Orchard > Setup > Build Tonic).", this);
        }
    }
}
