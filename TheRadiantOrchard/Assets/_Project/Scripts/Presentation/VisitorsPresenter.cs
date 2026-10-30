using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;
using UnityEngine.Pool;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows VisitorManager's visitors: takes a pooled VisitorView when one arrives (ENGINEERING_GUIDE 7), walks it
    /// spawn → waiting spot → exit in step with the manager's phases, keeps its patience bar current, and returns it
    /// to the pool when they leave. Movement is interpolated every frame from the manager's phase times, so walking is
    /// smooth although the manager ticks four times a second. Allocation-free per frame.
    /// </summary>
    public sealed class VisitorsPresenter : MonoBehaviour
    {
        private struct Shown
        {
            public int Id;
            public int Spot;
            public VisitorView View;
            public VisitorPhase Phase;
            public float Elapsed;
        }

        [SerializeField] private VisitorView _prefab;
        [Min(1)] [SerializeField] private int _prewarm = 4;
        [Tooltip("Where a tonic lands when nobody is waiting for it: this high above the first waiting spot (world units).")]
        [SerializeField] private float _spotTargetHeight = 1.2f;

        private readonly List<Shown> _shown = new List<Shown>(8);
        private ObjectPool<VisitorView> _pool;
        private VisitorManager _visitors;
        private GameConfig _config;
        private EventBus _events;
        private FarmView _farm;

        public int ShownCount => _shown.Count;

        public void Init(VisitorManager visitors, GameConfig config, EventBus events, FarmView farm)
        {
            _visitors = visitors;
            _config = config;
            _events = events;
            _farm = farm;
            EnsurePool();
            _events.Subscribe<VisitorArrived>(OnArrived);
            _events.Subscribe<VisitorLeft>(OnLeft);
        }

        /// <summary>The figure for this visitor, or null.</summary>
        public VisitorView ViewFor(int visitorId)
        {
            int i = IndexOf(visitorId);
            return i >= 0 ? _shown[i].View : null;
        }

        /// <summary>Where a tonic for this visitor should land; the first spot when they have no figure.</summary>
        public Vector2 TonicTarget(int visitorId)
        {
            VisitorView view = ViewFor(visitorId);
            return view != null ? view.TonicPoint : SpotTarget(0);
        }

        public Vector2 SpotTarget(int spot) =>
            (Vector2)_farm.WaitingSpots[Mathf.Clamp(spot, 0, _farm.WaitingSpots.Count - 1)].position + Vector2.up * _spotTargetHeight;

        private void OnArrived(VisitorArrived e)
        {
            VisitorView view = _pool.Get();
            view.Show(_config.FindFruit(e.FruitId));
            view.MoveTo(_farm.SpawnPoint.position);
            _shown.Add(new Shown { Id = e.VisitorId, Spot = e.Spot, View = view, Phase = VisitorPhase.WalkingIn });
        }

        private void OnLeft(VisitorLeft e)
        {
            int i = IndexOf(e.VisitorId);
            if (i < 0) return;
            _pool.Release(_shown[i].View);
            int last = _shown.Count - 1;
            _shown[i] = _shown[last];
            _shown.RemoveAt(last);
        }

        private void Update()
        {
            if (_visitors == null) return;
            BalanceConfig balance = _config.Balance;
            float dt = Time.deltaTime;

            for (int i = 0; i < _shown.Count; i++)
            {
                Shown s = _shown[i];
                if (!_visitors.TryGet(s.Id, out VisitorInfo info)) continue;

                // Follow the manager's phase changes exactly; between its ticks, advance locally for smooth motion.
                if (info.Phase != s.Phase)
                {
                    s.Phase = info.Phase;
                    s.Elapsed = info.PhaseElapsed;
                }
                else s.Elapsed += dt;

                Vector2 spot = _farm.WaitingSpots[s.Spot].position;
                switch (s.Phase)
                {
                    case VisitorPhase.WalkingIn:
                        s.View.MoveTo(Vector2.Lerp(_farm.SpawnPoint.position, spot, s.Elapsed / balance.VisitorWalkInSeconds));
                        break;
                    case VisitorPhase.Waiting:
                        s.View.MoveTo(spot);
                        s.View.SetPatience(info.Reserved ? info.Patience : 1f - s.Elapsed / balance.PatienceSeconds);
                        break;
                    case VisitorPhase.Leaving:
                        s.View.MoveTo(Vector2.Lerp(spot, _farm.ExitPoint.position, s.Elapsed / balance.VisitorWalkOutSeconds));
                        break;
                }
                _shown[i] = s;
            }
        }

        private int IndexOf(int visitorId)
        {
            for (int i = 0; i < _shown.Count; i++)
                if (_shown[i].Id == visitorId) return i;
            return -1;
        }

        private void EnsurePool()
        {
            if (_pool != null) return;
            _pool = new ObjectPool<VisitorView>(
                () => Instantiate(_prefab, transform),
                view => view.gameObject.SetActive(true),
                view => view.gameObject.SetActive(false),
                // Unity clears every pool when Play mode stops, after the scene has already destroyed the figures.
                view => { if (view != null) Destroy(view.gameObject); },
                collectionCheck: false);

            var warm = new VisitorView[_prewarm];
            for (int i = 0; i < warm.Length; i++) warm[i] = _pool.Get();
            for (int i = 0; i < warm.Length; i++) _pool.Release(warm[i]);
        }

        private void OnDestroy()
        {
            if (_events == null) return;
            _events.Unsubscribe<VisitorArrived>(OnArrived);
            _events.Unsubscribe<VisitorLeft>(OnLeft);
        }

        private void Awake()
        {
            if (_prefab == null)
                Log.Error($"VisitorsPresenter '{name}': the visitor prefab must be assigned; rebuild it (Radiant Orchard > Setup > Build Visitors).", this);
        }
    }
}
