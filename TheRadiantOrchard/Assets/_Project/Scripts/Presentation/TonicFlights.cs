using System;
using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;
using UnityEngine.Pool;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Turns every harvested fruit into a glowing tonic that arcs to its target and pops (CLAUDE.md 5.5). One runner
    /// moves all tonics in flight from a pooled set (ENGINEERING_GUIDE 7), so a flight allocates nothing per frame.
    /// Until visitors exist (W1·21) the target is the first waiting spot on the path. Arrived fires when a tonic
    /// reaches its target, for the heal to pick up.
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
        [Tooltip("Where on the target the tonic lands, relative to its feet (world units).")]
        [SerializeField] private Vector2 _targetOffset = new Vector2(0f, 1.2f);
        [Min(1)] [SerializeField] private int _prewarm = 4;

        private readonly List<Flight> _active = new List<Flight>(16);
        private ObjectPool<TonicView> _pool;
        private EventBus _events;
        private GameConfig _config;
        private FarmView _farm;
        private bool _subscribed;

        /// <summary>A tonic reached its target: fruit id and the landing point.</summary>
        public event Action<string, Vector2> Arrived;

        /// <summary>Where tonics fly. The first waiting spot until W1·21 points it at a visitor.</summary>
        public Transform Target { get; set; }

        public int ActiveCount => _active.Count;
        public int PooledCount => _pool != null ? _pool.CountAll : 0;

        public void Init(EventBus events, GameConfig config, FarmView farm)
        {
            _events = events;
            _config = config;
            _farm = farm;
            if (Target == null && farm.WaitingSpots.Count > 0) Target = farm.WaitingSpots[0];
            EnsurePool();
            Subscribe();
        }

        /// <summary>Sends one tonic from a point to a point. Harvests come through FruitHarvested; tests call this directly.</summary>
        public void Launch(Vector2 from, Vector2 to, Color colour, string fruitId)
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
                    Arrived?.Invoke(f.FruitId, f.End);
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

        private void OnHarvested(FruitHarvested e)
        {
            if (Target == null || e.Plot < 0 || e.Plot >= _farm.Plots.Count || _farm.Plots[e.Plot] == null) return;
            FruitDefinition fruit = _config.FindFruit(e.FruitId);
            Color colour = fruit != null ? fruit.Tint : Color.white;
            Vector2 target = (Vector2)Target.position + _targetOffset;
            Launch(_farm.Plots[e.Plot].FruitPosition, target, colour, e.FruitId);
        }

        private void EnsurePool()
        {
            if (_pool != null) return;
            _pool = new ObjectPool<TonicView>(
                () => Instantiate(_prefab, transform),
                view => view.gameObject.SetActive(true),
                view => view.gameObject.SetActive(false),
                view => Destroy(view.gameObject),
                collectionCheck: false);

            // Create the first few up front so the first harvests do not instantiate mid-game.
            var warm = new TonicView[_prewarm];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        }

        private void Subscribe()
        {
            if (_subscribed || _events == null || !isActiveAndEnabled) return;
            _events.Subscribe<FruitHarvested>(OnHarvested);
            _subscribed = true;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            if (!_subscribed) return;
            _events.Unsubscribe<FruitHarvested>(OnHarvested);
            _subscribed = false;
        }

        private void Awake()
        {
            if (_prefab == null)
                Log.Error($"TonicFlights '{name}': the tonic prefab must be assigned; rebuild it (Radiant Orchard > Setup > Build Tonic).", this);
        }
    }
}
