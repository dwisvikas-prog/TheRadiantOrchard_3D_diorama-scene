using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;
using UnityEngine.Pool;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows VisitorManager's visitors: takes a pooled VisitorView when one arrives (ENGINEERING_GUIDE 7), walks it
    /// in step with the manager's phases along the farm paths (jungle → entry route → waiting spot → exit route, or on to
    /// a bench seat, or back along the entry route into the jungle), keeps its patience bar current, and returns it to
    /// the pool when they leave. Each walk starts from wherever the figure is, so a change of plan never makes it jump.
    /// Movement is interpolated every frame from the manager's phase times (RouteMath), so walking is smooth although
    /// the manager ticks four times a second. Allocation-free per frame.
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
            // The current walk: from where the figure was, along the paths, to its goal (a buffer per figure).
            public Vector2[] Route;
            public int RouteCount;
            // Seconds the speech line has been up; negative when none is showing.
            public float LineAge;
        }

        [SerializeField] private VisitorView _prefab;
        [Min(1)] [SerializeField] private int _prewarm = 4;
        [Tooltip("Where a tonic lands when nobody is waiting for it: this high above the first waiting spot (world units).")]
        [SerializeField] private float _spotTargetHeight = 1.2f;
        [Tooltip("Distance between bench seats along the bench (world units); seats are centred on the bench.")]
        [SerializeField] private float _seatSpacing = 0.8f;
        [Tooltip("Seconds the arrival line stays up (from leaving the jungle), the last part fading.")]
        [Min(0.5f)] [SerializeField] private float _lineSeconds = 8f;
        [Min(0.05f)] [SerializeField] private float _lineFadeSeconds = 0.5f;

        // Longest walk: start + every route point + goal.
        private const int RouteCapacity = 16;

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
            if (!_visitors.TryGet(e.VisitorId, out VisitorInfo info)) return;
            VisitorView view = _pool.Get();
            view.Show(_config.FindFruit(e.FruitId));
            // Visitors who came while the app was closed are already standing at their spot.
            Vector2 at = info.Phase == VisitorPhase.WalkingIn ? (Vector2)_farm.SpawnPoint.position : (Vector2)_farm.WaitingSpots[e.Spot].position;
            view.MoveTo(at);
            var shown = new Shown { Id = e.VisitorId, Spot = e.Spot, View = view, Phase = info.Phase, Route = new Vector2[RouteCapacity], LineAge = -1f };
            PlanWalk(ref shown, info, at);
            if (!string.IsNullOrEmpty(info.LineKey))
            {
                view.ShowLine(_config.Strings.Get(info.LineKey));
                shown.LineAge = 0f;
            }
            _shown.Add(shown);
        }

        private void ShowExisting(VisitorInfo info)
        {
            VisitorView view = _pool.Get();
            view.Show(_config.FindFruit(info.FruitId));
            view.ShowPatienceBar(!info.PatienceRanOut);
            Vector2 at = info.Seat >= 0 ? SeatPosition(info.Seat) : (Vector2)_farm.SpawnPoint.position;
            view.MoveTo(at);
            view.gameObject.SetActive(info.Phase != VisitorPhase.Offscreen);
            var shown = new Shown { Id = info.Id, Spot = info.Spot, View = view, Phase = info.Phase, Elapsed = info.PhaseElapsed, Route = new Vector2[RouteCapacity], LineAge = -1f };
            PlanWalk(ref shown, info, at);
            _shown.Add(shown);
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
                    PlanWalk(ref s, info, s.View.transform.position);
                    if (info.PatienceRanOut) s.View.ShowPatienceBar(false);
                    // The line belongs to arriving; it goes once they move on (healed, or off to the bench).
                    if (s.Phase != VisitorPhase.WalkingIn && s.Phase != VisitorPhase.Waiting && s.LineAge >= 0f)
                    {
                        s.View.HideLine();
                        s.LineAge = -1f;
                    }
                    bool visible = s.Phase != VisitorPhase.Offscreen;
                    if (s.View.gameObject.activeSelf != visible) s.View.gameObject.SetActive(visible);
                }
                else s.Elapsed += dt;

                if (s.LineAge >= 0f)
                {
                    s.LineAge += dt;
                    if (s.LineAge >= _lineSeconds)
                    {
                        s.View.HideLine();
                        s.LineAge = -1f;
                    }
                    else s.View.SetLineAlpha((_lineSeconds - s.LineAge) / _lineFadeSeconds);
                }

                switch (s.Phase)
                {
                    case VisitorPhase.WalkingIn:
                    case VisitorPhase.WalkingAway:
                        s.View.MoveTo(RouteMath.PointAt(s.Route, s.RouteCount, s.Elapsed / balance.VisitorWalkInSeconds));
                        break;
                    case VisitorPhase.Waiting:
                        s.View.MoveTo(_farm.WaitingSpots[s.Spot].position);
                        s.View.SetPatience(info.Reserved ? info.Patience : 1f - s.Elapsed / info.PatienceSeconds);
                        break;
                    case VisitorPhase.WalkingToBench:
                        s.View.MoveTo(RouteMath.PointAt(s.Route, s.RouteCount, s.Elapsed / balance.VisitorWalkToBenchSeconds));
                        break;
                    case VisitorPhase.Sitting:
                        s.View.MoveTo(SeatPosition(info.Seat));
                        break;
                    case VisitorPhase.Leaving:
                        s.View.MoveTo(RouteMath.PointAt(s.Route, s.RouteCount, s.Elapsed / balance.VisitorWalkOutSeconds));
                        break;
                }
                _shown[i] = s;
            }
        }

        // Lays out the walk for the visitor's phase from where the figure is: along the entry route to their spot, back
        // along it into the jungle, along the exit route out, or straight to a bench seat (all inside the centre square).
        private void PlanWalk(ref Shown s, VisitorInfo info, Vector2 from)
        {
            s.RouteCount = 0;
            Add(ref s, from);
            switch (info.Phase)
            {
                case VisitorPhase.WalkingIn:
                    for (int i = 1; i < _farm.EntryRoute.Count; i++) Add(ref s, _farm.EntryRoute[i]);
                    Add(ref s, _farm.WaitingSpots[s.Spot].position);
                    break;
                case VisitorPhase.WalkingAway:
                    for (int i = _farm.EntryRoute.Count - 1; i >= 0; i--) Add(ref s, _farm.EntryRoute[i]);
                    break;
                case VisitorPhase.Leaving:
                    for (int i = 0; i < _farm.ExitRoute.Count; i++) Add(ref s, _farm.ExitRoute[i]);
                    break;
                case VisitorPhase.WalkingToBench:
                    Add(ref s, SeatPosition(info.Seat));
                    break;
            }
        }

        private static void Add(ref Shown s, Vector2 point)
        {
            if (s.RouteCount < s.Route.Length) s.Route[s.RouteCount++] = point;
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
