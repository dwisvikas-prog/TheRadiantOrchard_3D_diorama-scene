using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;

namespace RadiantOrchard.App
{
    /// <summary>
    /// Shows DeliveryService's decisions (CLAUDE.md 5.5): a tonic flies from the tree (or the HUD basket) to the visitor
    /// it was sent to, or from the tree into the basket; a landing on a visitor is reported back so they are healed. A
    /// harvest held back because the basket is full shows a short note over the tree. Lives in the composition root
    /// because it joins Gameplay, Presentation and the HUD. Dispose to unsubscribe.
    /// </summary>
    public sealed class HarvestDelivery : IDisposable
    {
        public const string BasketFullKey = "ui.basket.full";
        private const int IntoBasket = -1;

        private readonly EventBus _events;
        private readonly DeliveryService _delivery;
        private readonly VisitorsPresenter _visitorViews;
        private readonly TonicFlights _tonics;
        private readonly VirtuePopups _popups;
        private readonly FarmView _farm;
        private readonly BasketView _basket;
        private readonly Camera _camera;
        private readonly GameConfig _config;

        public HarvestDelivery(EventBus events, DeliveryService delivery, VisitorsPresenter visitorViews, TonicFlights tonics,
            VirtuePopups popups, FarmView farm, BasketView basket, Camera camera, GameConfig config)
        {
            _events = events;
            _delivery = delivery;
            _visitorViews = visitorViews;
            _tonics = tonics;
            _popups = popups;
            _farm = farm;
            _basket = basket;
            _camera = camera;
            _config = config;
            _events.Subscribe<DeliveryStarted>(OnDeliveryStarted);
            _events.Subscribe<FruitBasketed>(OnBasketed);
            _events.Subscribe<HarvestHeld>(OnHeld);
            _tonics.Arrived += OnTonicArrived;
        }

        public void Dispose()
        {
            _events.Unsubscribe<DeliveryStarted>(OnDeliveryStarted);
            _events.Unsubscribe<FruitBasketed>(OnBasketed);
            _events.Unsubscribe<HarvestHeld>(OnHeld);
            _tonics.Arrived -= OnTonicArrived;
        }

        /// <summary>Where the HUD basket is in the world right now; tonics fly to and from it.</summary>
        public Vector2 BasketWorldPoint()
        {
            Vector2 screen = _basket.AnchorScreenPoint;
            return _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
        }

        private void OnDeliveryStarted(DeliveryStarted e)
        {
            Vector2 from = e.FromPlot == DeliveryService.FromBasket ? BasketWorldPoint() : FruitPoint(e.FromPlot);
            _tonics.Launch(from, _visitorViews.TonicTarget(e.VisitorId), Tint(e.FruitId), e.FruitId, e.VisitorId);
        }

        private void OnBasketed(FruitBasketed e) =>
            _tonics.Launch(FruitPoint(e.FromPlot), BasketWorldPoint(), Tint(e.FruitId), e.FruitId, IntoBasket);

        private void OnHeld(HarvestHeld e) =>
            _popups.Show(_config.Strings.Get(BasketFullKey), Tint(e.FruitId), FruitPoint(e.Plot));

        private void OnTonicArrived(string fruitId, int visitor, Vector2 point)
        {
            if (visitor != IntoBasket) _delivery.Complete(visitor, fruitId);
        }

        // The fruit on that plot's tree; the basket when the plot is unknown (fruit from code, not a tree).
        private Vector2 FruitPoint(int plot) =>
            plot >= 0 && plot < _farm.Plots.Count && _farm.Plots[plot] != null ? _farm.Plots[plot].FruitPosition : BasketWorldPoint();

        private Color Tint(string fruitId)
        {
            FruitDefinition fruit = _config.FindFruit(fruitId);
            return fruit != null ? fruit.Tint : Color.white;
        }
    }
}
