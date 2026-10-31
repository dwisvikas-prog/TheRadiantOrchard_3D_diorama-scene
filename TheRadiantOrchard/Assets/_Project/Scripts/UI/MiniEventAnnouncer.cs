using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// Mini-event banners (CLAUDE.md 5.11) on the shared banner (UI-09): the event's title and line from its definition,
    /// with {0} = its duration as m:ss. Dispose to unsubscribe.
    /// </summary>
    public sealed class MiniEventAnnouncer : IDisposable
    {
        private readonly ZoneBannerView _banner;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        private readonly float _seconds;

        public MiniEventAnnouncer(ZoneBannerView banner, GameConfig config, EventBus events, float bannerSeconds)
        {
            _banner = banner != null ? banner : throw new ArgumentNullException(nameof(banner));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _seconds = bannerSeconds;
            _events.Subscribe<MiniEventStarted>(OnStarted);
        }

        public void Dispose() => _events.Unsubscribe<MiniEventStarted>(OnStarted);

        private void OnStarted(MiniEventStarted e)
        {
            MiniEventDefinition definition = null;
            foreach (MiniEventDefinition d in _config.MiniEvents)
                if (d != null && d.Id == e.Id) definition = d;
            if (definition == null) return;

            StringTable strings = _config.Strings;
            string duration = GrowthTimerText.Format((int)Math.Ceiling(definition.DurationSeconds), strings);
            _banner.ShowText(strings.Get(definition.TitleKey), string.Format(strings.Get(definition.BodyKey), duration), _seconds);
        }
    }
}
