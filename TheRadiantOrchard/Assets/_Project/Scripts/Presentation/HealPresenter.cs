using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Plays a heal: the healed visitor turns to colour and the virtue name pops up above them (CLAUDE.md 5.6).
    /// Dev heals with nobody on the path pop up over the first waiting spot. Dispose to unsubscribe.
    /// </summary>
    public sealed class HealPresenter : IDisposable
    {
        private readonly VisitorsPresenter _visitors;
        private readonly VirtuePopups _popups;
        private readonly GameConfig _config;
        private readonly EventBus _events;

        public HealPresenter(VisitorsPresenter visitors, VirtuePopups popups, GameConfig config, EventBus events)
        {
            _visitors = visitors != null ? visitors : throw new ArgumentNullException(nameof(visitors));
            _popups = popups != null ? popups : throw new ArgumentNullException(nameof(popups));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _events.Subscribe<VisitorHealed>(OnHealed);
        }

        public void Dispose() => _events.Unsubscribe<VisitorHealed>(OnHealed);

        private void OnHealed(VisitorHealed e)
        {
            FruitDefinition fruit = _config.FindFruit(e.FruitId);
            Color colour = fruit != null ? fruit.Tint : Color.white;
            VisitorView view = _visitors.ViewFor(e.VisitorId);
            if (view != null) view.PlayHeal(colour);
            _popups.Show(_config.Strings.Get(e.VirtueNameKey), colour, view != null ? view.PopupPoint : _visitors.SpotTarget(0));
        }
    }
}
