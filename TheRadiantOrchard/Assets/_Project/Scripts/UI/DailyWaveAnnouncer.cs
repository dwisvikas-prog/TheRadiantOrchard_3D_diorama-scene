using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// The daily visitor wave's banners (CLAUDE.md 5.10) on the shared banner (UI-09): "Daily visitors!" with the bonus
    /// when they walk in, and the bonus paid once all are healed. Dispose to unsubscribe.
    /// </summary>
    public sealed class DailyWaveAnnouncer : IDisposable
    {
        public const string TitleKey = "ui.wave.title";
        public const string BodyKey = "ui.wave.body";
        public const string DoneTitleKey = "ui.wave.done_title";
        public const string DoneBodyKey = "ui.wave.done_body";

        private readonly ZoneBannerView _banner;
        private readonly StringTable _strings;
        private readonly EventBus _events;
        private readonly float _seconds;

        public DailyWaveAnnouncer(ZoneBannerView banner, StringTable strings, EventBus events, float bannerSeconds)
        {
            _banner = banner != null ? banner : throw new ArgumentNullException(nameof(banner));
            _strings = strings != null ? strings : throw new ArgumentNullException(nameof(strings));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _seconds = bannerSeconds;
            _events.Subscribe<DailyWaveStarted>(OnStarted);
            _events.Subscribe<DailyWaveCompleted>(OnCompleted);
        }

        public void Dispose()
        {
            _events.Unsubscribe<DailyWaveStarted>(OnStarted);
            _events.Unsubscribe<DailyWaveCompleted>(OnCompleted);
        }

        private void OnStarted(DailyWaveStarted e) =>
            _banner.ShowText(_strings.Get(TitleKey), string.Format(_strings.Get(BodyKey), e.Visitors, e.BonusSeeds), _seconds);

        private void OnCompleted(DailyWaveCompleted e) =>
            _banner.ShowText(_strings.Get(DoneTitleKey), string.Format(_strings.Get(DoneBodyKey), e.BonusSeeds), _seconds);
    }
}
