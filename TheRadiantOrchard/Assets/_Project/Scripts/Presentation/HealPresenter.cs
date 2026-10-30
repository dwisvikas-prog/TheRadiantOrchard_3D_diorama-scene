using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>Plays a heal: the visitor turns to colour and the virtue name pops up above them. Dispose to unsubscribe.</summary>
    public sealed class HealPresenter : IDisposable
    {
        private readonly PlaceholderVisitor _visitor;
        private readonly VirtuePopups _popups;
        private readonly GameConfig _config;
        private readonly EventBus _events;

        public HealPresenter(PlaceholderVisitor visitor, VirtuePopups popups, GameConfig config, EventBus events)
        {
            _visitor = visitor ?? throw new ArgumentNullException(nameof(visitor));
            _popups = popups ?? throw new ArgumentNullException(nameof(popups));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _events.Subscribe<VisitorHealed>(OnHealed);
        }

        public void Dispose() => _events.Unsubscribe<VisitorHealed>(OnHealed);

        private void OnHealed(VisitorHealed e)
        {
            FruitDefinition fruit = _config.FindFruit(e.FruitId);
            Color colour = fruit != null ? fruit.Tint : Color.white;
            _visitor.PlayHeal(colour);
            _popups.Show(_config.Strings.Get(e.VirtueNameKey), colour, _visitor.PopupPosition);
        }
    }
}
