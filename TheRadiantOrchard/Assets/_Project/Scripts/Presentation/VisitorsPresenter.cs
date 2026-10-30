using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;
using UnityEngine.Pool;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows VisitorManager's visitors: takes a pooled VisitorView when one arrives (ENGINEERING_GUIDE 7), walks it
    /// in step with the manager's phases (spawn → waiting spot → exit, or on to a bench seat or back into the jungle),
    /// keeps its patience bar current, and returns it to the pool when they leave. Each walk starts from wherever the
    /// figure is, so a change of plan never makes it jump. Movement is interpolated every frame from the manager's
    /// phase times, so walking is smooth although the manager ticks four times a second. Allocation-free per frame.
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
            // Where the current walk started.
            public Vector2 From;
        }

        [SerializeField] private VisitorView _prefab;
        [Min(1)] [SerializeField] private int _prewarm = 4;
        [Tooltip("Where a tonic lands when nobody is waiting for it: this high above the first waiting spot (world units).")]
        [SerializeField] private float _spotTargetHeight = 1.2f;
        [Tooltip("Distance between bench seats along the bench (world units); seats are centred on the bench.")]
        [SerializeField] private float _seatSpacing = 0.8f;

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
            // Visitors restored from the save (the bench) are already there before any event.
            for (int i = 0; i < _visitors.Count; i++) ShowExisting(_visitors.GetAt(i));
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

        public Vector2 SeatPosition(int seat)
        {
            float offset = (seat - (_visitors.SeatCount - 1) * 0.5f) * _seatSpacing;
            return (Vector2)_farm.Bench.position + Vector2.right * offset;
        }

        private void OnArrived(VisitorArrived e)
        {
            VisitorView view = _pool.Get();
            view.Show(_config.FindFruit(e.FruitId));
            view.MoveTo(_farm.SpawnPoint.position);
            _shown.Add(new Shown { Id = e.VisitorId, Spot = e.Spot, View = view, Phase = VisitorPhase.WalkingIn, From = _farm.SpawnPoint.position });
        }

        private void ShowExisting(VisitorInfo info)
        {
            VisitorView view = _pool.Get();
            view.Show(_config.FindFruit(info.FruitId));
            view.ShowPatienceBar(!info.PatienceRanOut);
            Vector2 at = info.Seat >= 0 ? SeatPosition(info.Seat) : (Vector2)_farm.SpawnPoint.position;
            view.MoveTo(at);
            view.gameObject.SetActive(info.Phase != VisitorPhase.Offscreen);
            _shown.Add(new Shown { Id = info.Id, Spot = info.Spot, View = view, Phase = info.Phase, Elapsed = info.PhaseElapsed, From = at });
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
                    s.From = s.View.transform.position;
                    if (info.PatienceRanOut) s.View.ShowPatienceBar(false);
                    bool visible = s.Phase != VisitorPhase.Offscreen;
                    if (s.View.gameObject.activeSelf != visible) s.View.gameObject.SetActive(visible);
                }
                else s.Elapsed += dt;

                switch (s.Phase)
                {
                    case VisitorPhase.WalkingIn:
                        s.View.MoveTo(Vector2.Lerp(s.From, _farm.WaitingSpots[s.Spot].position, s.Elapsed / balance.VisitorWalkInSeconds));
                        break;
                    case VisitorPhase.Waiting:
                        s.View.MoveTo(_farm.WaitingSpots[s.Spot].position);
                        s.View.SetPatience(info.Reserved ? info.Patience : 1f - s.Elapsed / balance.PatienceSeconds);
                        break;
                    case VisitorPhase.WalkingToBench:
                        s.View.MoveTo(Vector2.Lerp(s.From, SeatPosition(info.Seat), s.Elapsed / balance.VisitorWalkToBenchSeconds));
                        break;
                    case VisitorPhase.Sitting:
                        s.View.MoveTo(SeatPosition(info.Seat));
                        break;
                    case VisitorPhase.WalkingAway:
                        s.View.MoveTo(Vector2.Lerp(s.From, _farm.SpawnPoint.position, s.Elapsed / balance.VisitorWalkInSeconds));
                        break;
                    case VisitorPhase.Leaving:
                        s.View.MoveTo(Vector2.Lerp(s.From, _farm.ExitPoint.position, s.Elapsed / balance.VisitorWalkOutSeconds));
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
