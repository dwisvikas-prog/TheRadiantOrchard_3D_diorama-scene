using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Plays a heal (CLAUDE.md 5.6): the healed visitor turns to colour and hops, the virtue name pops up above them and
    /// the seeds paid just below it, in gold when a bonus (golden fruit, Golden Harvest, combo) raised them. Dev heals
    /// with nobody on the path pop up over the first waiting spot. Dispose to unsubscribe.
    /// </summary>
    public sealed class HealPresenter : IDisposable
    {
        public const string SeedsKey = "ui.heal.seeds";
        private static readonly Color PlainSeedsColour = new Color(1f, 0.97f, 0.88f, 1f);
        private static readonly Vector2 SeedsOffset = new Vector2(0f, -0.65f);

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
            Vector2 point = view != null ? view.PopupPoint : _visitors.SpotTarget(0);
            _popups.Show(_config.Strings.Get(e.VirtueNameKey), colour, point);
            _popups.Show(string.Format(_config.Strings.Get(SeedsKey), e.Seeds), e.HasBonus ? GoldenLook.Tint : PlainSeedsColour, point + SeedsOffset);
        }
    }
}
