using System;
using RadiantOrchard.Core;

namespace RadiantOrchard.Gameplay
{
    /// <summary>
    /// Where harvested fruit goes (CLAUDE.md 5.5). A harvest is sent to the visitor DeliveryTargeting picks; with nobody
    /// to take it, it goes into the basket (BasketMaxPerFruit per fruit, saved in GameState). Every GameClock tick the
    /// basket hands fruit to matching visitors as soon as they can be served. A harvest is only allowed when the fruit
    /// has somewhere to go (CanTake, PlotManager's harvest gate): with the basket full and nobody asking, it stays on
    /// the tree, so fruit is never thrown away. Views fly the tonics from DeliveryStarted / FruitBasketed and report
    /// landings through Complete. Golden fruit stay golden in the basket and leave it first. Dispose to unsubscribe.
    /// </summary>
    public sealed class DeliveryService : IDisposable
    {
        /// <summary>DeliveryStarted.FromPlot for fruit that comes out of the basket.</summary>
        public const int FromBasket = -1;

        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly VisitorManager _visitors;
        private readonly Action _markDirty;

        public DeliveryService(GameState state, GameConfig config, EventBus events, VisitorManager visitors, Action markDirty)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _visitors = visitors ?? throw new ArgumentNullException(nameof(visitors));
            _markDirty = markDirty ?? throw new ArgumentNullException(nameof(markDirty));
            _events.Subscribe<FruitHarvested>(OnHarvested);
        }

        public void Dispose() => _events.Unsubscribe<FruitHarvested>(OnHarvested);

        public int BasketCount(string fruitId) => _state.GetBasketCount(fruitId);

        /// <summary>Whether a fruit picked now has somewhere to go: a visitor who wants it, or room in the basket.</summary>
        public bool CanTake(string fruitId) =>
            DeliveryTargeting.Pick(_visitors, fruitId) != DeliveryTargeting.Nobody
            || BasketCount(fruitId) < _config.Balance.BasketMaxPerFruit;

        /// <summary>Called on every GameClock tick: one fruit of each kind in the basket goes to a visitor who can take it.</summary>
        public void Tick()
        {
            for (int i = 0; i < _config.Fruits.Count; i++)
            {
                string fruitId = _config.Fruits[i].Id;
                int count = BasketCount(fruitId);
                if (count == 0) continue;
                int visitor = Reserve(fruitId);
                if (visitor == DeliveryTargeting.Nobody) continue;

                int golden = _state.GetBasketGoldenCount(fruitId);
                bool isGolden = golden > 0;
                SetBasket(fruitId, count - 1, isGolden ? golden - 1 : golden);
                _events.Publish(new DeliveryStarted(FromBasket, fruitId, visitor, isGolden));
            }
        }

        /// <summary>
        /// A tonic reached its visitor: heals them. If they cannot take it after all, the fruit goes back into the basket
        /// (when there is room). Returns the seeds paid.
        /// </summary>
        public int Complete(int visitorId, string fruitId, bool golden = false)
        {
            int seeds = _visitors.Deliver(visitorId, fruitId, golden);
            if (seeds > 0) return seeds;

            int count = BasketCount(fruitId);
            if (count < _config.Balance.BasketMaxPerFruit) AddToBasket(fruitId, golden);
            else Log.Warn($"DeliveryService: visitor {visitorId} could not take {fruitId} and the basket is full; the fruit is lost.");
            return 0;
        }

        private void OnHarvested(FruitHarvested e)
        {
            int visitor = Reserve(e.FruitId);
            if (visitor != DeliveryTargeting.Nobody)
            {
                _events.Publish(new DeliveryStarted(e.Plot, e.FruitId, visitor, e.IsGolden));
                return;
            }

            int count = BasketCount(e.FruitId);
            if (count >= _config.Balance.BasketMaxPerFruit)
            {
                // The harvest gate keeps this from happening in play; only code that bypasses it gets here.
                Log.Warn($"DeliveryService: {e.FruitId} was harvested with nobody to take it and a full basket; the fruit is lost.");
                return;
            }
            AddToBasket(e.FruitId, e.IsGolden);
            _events.Publish(new FruitBasketed(e.Plot, e.FruitId, count + 1, e.IsGolden));
        }

        private int Reserve(string fruitId)
        {
            int visitor = DeliveryTargeting.Pick(_visitors, fruitId);
            return visitor != DeliveryTargeting.Nobody && _visitors.TryReserve(visitor) ? visitor : DeliveryTargeting.Nobody;
        }

        private void AddToBasket(string fruitId, bool golden)
        {
            int golds = _state.GetBasketGoldenCount(fruitId);
            SetBasket(fruitId, BasketCount(fruitId) + 1, golden ? golds + 1 : golds);
        }

        private void SetBasket(string fruitId, int count, int golden)
        {
            _state.SetBasketCount(fruitId, count, golden);
            _markDirty();
            _events.Publish(new BasketChanged(fruitId, count));
        }
    }
}
