using System;
using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Keeps every PlotView in step with PlotManager by listening to plot events, and updates the growth countdowns
    /// on each GameClock tick (wired by the GameInstaller). Dispose to unsubscribe.
    /// </summary>
    public sealed class PlotsPresenter : IDisposable
    {
        private readonly IReadOnlyList<PlotView> _views;
        private readonly PlotManager _plots;
        private readonly GameConfig _config;
        private readonly EventBus _events;
        // Seconds each countdown currently shows (-1 = none), so text is only rebuilt when the shown second changes.
        private readonly int[] _shownSeconds;

        public PlotsPresenter(IReadOnlyList<PlotView> views, PlotManager plots, GameConfig config, EventBus events)
        {
            _views = views ?? throw new ArgumentNullException(nameof(views));
            _plots = plots ?? throw new ArgumentNullException(nameof(plots));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _events = events ?? throw new ArgumentNullException(nameof(events));

            _shownSeconds = new int[_views.Count];
            if (_views.Count != _plots.Count)
                Log.Error($"PlotsPresenter: {_views.Count} plot views for {_plots.Count} plots; rebuild the farm.");

            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null) _views[i].Bind(i, _plots);

            _events.Subscribe<PlotStageChanged>(OnStageChanged);
            _events.Subscribe<PlotUnlocked>(OnUnlocked);
            RefreshAll();
        }

        public void RefreshAll()
        {
            for (int i = 0; i < _views.Count && i < _plots.Count; i++) Refresh(i);
        }

        /// <summary>Called on every GameClock tick, after PlotManager.Tick.</summary>
        public void Tick()
        {
            for (int i = 0; i < _views.Count && i < _plots.Count; i++)
                if (_views[i] != null) UpdateTimer(i, _plots.Get(i));
        }

        public void Dispose()
        {
            _events.Unsubscribe<PlotStageChanged>(OnStageChanged);
            _events.Unsubscribe<PlotUnlocked>(OnUnlocked);
        }

        private void OnStageChanged(PlotStageChanged e) => Refresh(e.Plot);

        private void OnUnlocked(PlotUnlocked e) => Refresh(e.Plot);

        private void Refresh(int index)
        {
            if (index < 0 || index >= _views.Count || _views[index] == null) return;
            PlotInfo info = _plots.Get(index);
            _views[index].Show(info, _config.FindFruit(info.FruitId));
            _shownSeconds[index] = -1;
            UpdateTimer(index, info);
        }

        private void UpdateTimer(int index, PlotInfo info)
        {
            int seconds = info.IsGrowing ? info.SecondsUntilRipe : -1;
            if (seconds == _shownSeconds[index]) return;
            _shownSeconds[index] = seconds;
            if (seconds >= 0) _views[index].SetTimerText(GrowthTimerText.Format(seconds, _config.Strings));
        }
    }
}
