using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using UnityEngine;

namespace RadiantOrchard.App
{
    /// <summary>
    /// Harvest → tonic → heal (CLAUDE.md 5.5): each harvested fruit is given to the longest-waiting visitor who asked
    /// for it (VisitorManager.Reserve), flies there as a tonic, and heals them on arrival. A fruit nobody asked for pops
    /// over the first waiting spot without a reward until the basket (W2·07), which also brings bench targeting.
    /// Lives in the composition root because it joins Gameplay to Presentation. Dispose to unsubscribe.
    /// </summary>
    public sealed class HarvestDelivery : IDisposable
    {
        private const int NoVisitor = -1;

        private readonly EventBus _events;
        private readonly VisitorManager _visitors;
        private readonly VisitorsPresenter _visitorViews;
        private readonly TonicFlights _tonics;
        private readonly FarmView _farm;
        private readonly GameConfig _config;

        public HarvestDelivery(EventBus events, VisitorManager visitors, VisitorsPresenter visitorViews, TonicFlights tonics, FarmView farm, GameConfig config)
        {
            _events = events;
            _visitors = visitors;
            _visitorViews = visitorViews;
            _tonics = tonics;
            _farm = farm;
            _config = config;
            _events.Subscribe<FruitHarvested>(OnHarvested);
            _tonics.Arrived += OnTonicArrived;
        }

        public void Dispose()
        {
            _events.Unsubscribe<FruitHarvested>(OnHarvested);
            _tonics.Arrived -= OnTonicArrived;
        }

        private void OnHarvested(FruitHarvested e)
        {
            if (e.Plot < 0 || e.Plot >= _farm.Plots.Count || _farm.Plots[e.Plot] == null) return;
            int visitor = _visitors.Reserve(e.FruitId);
            Vector2 target = visitor != NoVisitor ? _visitorViews.TonicTarget(visitor) : _visitorViews.SpotTarget(0);
            FruitDefinition fruit = _config.FindFruit(e.FruitId);
            _tonics.Launch(_farm.Plots[e.Plot].FruitPosition, target, fruit != null ? fruit.Tint : Color.white, e.FruitId, visitor);
        }

        private void OnTonicArrived(string fruitId, int visitor, Vector2 point)
        {
            if (visitor != NoVisitor) _visitors.Deliver(visitor, fruitId);
        }
    }
}
