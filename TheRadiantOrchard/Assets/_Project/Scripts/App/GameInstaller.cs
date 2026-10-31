using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Input;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RadiantOrchard.App
{
    /// <summary>
    /// The one wiring object in the Game scene. Creates the gameplay systems, passes them the services from Boot and
    /// connects their views. Never searches for objects at runtime.
    /// </summary>
    public sealed class GameInstaller : MonoBehaviour
    {
        [SerializeField] private string _bootSceneName = "Boot";
        [SerializeField] private FarmView _farmView;
        [SerializeField] private CameraController _cameraController;
        [SerializeField] private GestureRecognizer _gestures;
        [SerializeField] private PlantMenuView _plantMenu;
        [SerializeField] private TonicFlights _tonics;
        [SerializeField] private VisitorsPresenter _visitorViews;
        [SerializeField] private VirtuePopups _popups;
        [SerializeField] private HudView _hud;
        [SerializeField] private BasketView _basket;
        [SerializeField] private ComboView _combo;
        [SerializeField] private VibrancyMeterView _meter;
        [SerializeField] private ZoneBannerView _banner;
        [SerializeField] private ZoneUnlockSequence _zoneUnlock;
        [SerializeField] private QuizPanelView _quizPanel;
        [SerializeField] private GoldenHarvestView _goldenHarvest;
        [Tooltip("How long the daily wave's and mini-events' banners stay up (seconds).")]
        [Min(0.5f)] [SerializeField] private float _waveBannerSeconds = 3.5f;

        private AppServices _services;
        private PlotManager _plots;
        private PlotsPresenter _plotsPresenter;
        private FarmPointerHandler _pointer;
        private HealService _heal;
        private BoostService _boosts;
        private HealPresenter _healPresenter;
        private VisitorManager _visitors;
        private DeliveryService _delivery;
        private HarvestDelivery _deliveryViews;
        private VibrancyManager _vibrancy;
        private ZonesPresenter _zones;
        private QuizManager _quiz;
        private WisdomTreePresenter _treePresenter;
        private DailyWave _dailyWave;
        private DailyWaveAnnouncer _waveAnnouncer;
        private MiniEventManager _miniEvents;
        private MiniEventAnnouncer _eventAnnouncer;

        private void Awake()
        {
            AppServices services = AppServices.Current;
            if (services == null)
            {
                // Play was pressed with Game open: boot first, so every session starts the same way.
                SceneManager.LoadScene(_bootSceneName);
                return;
            }

            if (_farmView == null || _cameraController == null || _gestures == null || _plantMenu == null || _tonics == null
                || _visitorViews == null || _popups == null || _hud == null || _basket == null || _combo == null || _meter == null
                || _banner == null || _zoneUnlock == null || _quizPanel == null || _goldenHarvest == null)
            {
                Log.Error("GameInstaller: Farm View, Camera Controller, Gestures, Plant Menu, Tonics, Visitor Views, Popups, HUD, Basket, Combo, Meter, Banner, Zone Unlock, Quiz Panel and Golden Harvest must be assigned (Radiant Orchard > Setup).", this);
                return;
            }

            _services = services;
            _cameraController.SetBounds(_farmView.WorldBounds);
            _cameraController.Init(services.Config.Balance);

            _plots = new PlotManager(services.State, services.Config, services.Time, services.Events, services.Save.MarkDirty);
            // Before the plot views: a save that is ahead of its stage opens its plots first.
            _vibrancy = new VibrancyManager(services.State, services.Config, services.Events, _plots, services.Save.MarkDirty);
            _plotsPresenter = new PlotsPresenter(_farmView.Plots, _plots, services.Config, services.Events);
            services.Clock.Ticked += _plots.Tick;
            services.Clock.Ticked += _plotsPresenter.Tick;
            _boosts = new BoostService(services.Events);
            services.Clock.Ticked += TickBoosts;
            _quiz = new QuizManager(services.State, services.Config, services.Events, _boosts, services.Save.MarkDirty, new System.Random());
            _heal = new HealService(services.State, services.Config, services.Events, services.Save.MarkDirty, services.Time, _boosts);
            _visitors = new VisitorManager(services.State, services.Config, services.Time, services.Events, _heal, services.Save.MarkDirty, new System.Random());
            _dailyWave = new DailyWave(services.State, services.Config, services.Time, services.Events, _visitors, services.Save.MarkDirty);
            _miniEvents = new MiniEventManager(services.State, services.Config, services.Events, services.Save.MarkDirty, new System.Random(),
                new GoldenHourEffect(_boosts), new VisitorRushEffect(_visitors), new BlessingRainEffect(_plots));
            services.Clock.Ticked += TickVisitors;
            _delivery = new DeliveryService(services.State, services.Config, services.Events, _visitors, services.Save.MarkDirty);
            _plots.HarvestGate = _delivery.CanTake;
            // After the visitors' tick, so a visitor who just reached their spot gets basket fruit the same tick.
            services.Clock.Ticked += _delivery.Tick;
            _visitorViews.Init(_visitors, services.Config, services.Events, _farmView);
            _healPresenter = new HealPresenter(_visitorViews, _popups, services.Config, services.Events);
            _hud.Init(services.State, services.Events, services.Config.Strings);
            _basket.Init(services.Config, services.State, services.Events);
            _combo.Init(services.Config, services.Events);
            _meter.Init(services.Config, services.State, services.Events);
            _zones = new ZonesPresenter(_farmView, services.State, services.Events);
            _treePresenter = new WisdomTreePresenter(_farmView.WisdomTreeView, _quiz, services.Events);
            _goldenHarvest.Init(services.Config, _boosts, services.Events);
            _deliveryViews = new HarvestDelivery(services.Events, _delivery, _visitorViews, _tonics, _popups, _farmView, _basket,
                _cameraController.GetComponent<Camera>(), services.Config);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevGame.Current = new DevGame(services.Config, services.State, services.Time, services.Events, services.Save, _plots, _heal, _visitors, _delivery, _boosts, _vibrancy, _quiz, _dailyWave, _miniEvents);
#endif

            _gestures.Init(services.Config.Balance, _cameraController);
            _plantMenu.Init(_plots, services.Config, services.State, services.Events);
            _quizPanel.Init(_quiz, services.Config);
            _banner.Init(services.Config.Strings);
            _waveAnnouncer = new DailyWaveAnnouncer(_banner, services.Config.Strings, services.Events, _waveBannerSeconds);
            _eventAnnouncer = new MiniEventAnnouncer(_banner, services.Config, services.Events, _waveBannerSeconds);
            // After the zones presenter, so a zone is marked pending before its sequence looks at it.
            _zoneUnlock.Init(services.Config, services.Events, _cameraController, _gestures, _zones, _banner, _plantMenu, _quizPanel);
            _pointer = new FarmPointerHandler(_gestures, _cameraController.GetComponent<Camera>(), _farmView, services.Config.Farm,
                _plots, _plantMenu, _quiz, _quizPanel, services.Config.Balance.SwipeSampleSpacing);

            // Once every view is listening: a new day's wave first, so it gets the empty path; then visitors who came
            // while the app was closed (or in the background) take any spots left.
            ArriveAfterAway(services.LaunchTimeAway);
            services.Events.Subscribe<TimeAwayApplied>(OnTimeAway);
            services.Time.DevOffsetChanged += OnDevOffsetChanged;

            Log.Info($"GameInstaller: ready; {services.State.Seeds} seeds, {services.State.HealedTotal} healed, away {services.LaunchTimeAway}.");
        }

        private void TickVisitors()
        {
            _visitors.Tick(_services.Clock.IntervalSeconds);
            _dailyWave.Tick();
        }

        private void ArriveAfterAway(System.TimeSpan away)
        {
            _dailyWave.CheckNewDay();
            _visitors.ArriveAfterAway(away);
        }

        // The dev panel's time skip can cross midnight; the wave follows it as it would a relaunch.
        private void OnDevOffsetChanged(System.TimeSpan offset) => _dailyWave.CheckNewDay();

        private void TickBoosts() => _boosts.Tick(_services.Clock.IntervalSeconds);

        private void OnTimeAway(TimeAwayApplied e) => ArriveAfterAway(e.Elapsed);

        private void OnDestroy()
        {
            if (_services == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevGame.Current != null && DevGame.Current.Plots == _plots) DevGame.Current = null;
#endif
            if (_plots != null) _services.Clock.Ticked -= _plots.Tick;
            if (_plotsPresenter != null) _services.Clock.Ticked -= _plotsPresenter.Tick;
            if (_delivery != null) _services.Clock.Ticked -= _delivery.Tick;
            if (_boosts != null) _services.Clock.Ticked -= TickBoosts;
            if (_visitors != null)
            {
                _services.Clock.Ticked -= TickVisitors;
                _services.Events.Unsubscribe<TimeAwayApplied>(OnTimeAway);
                _services.Time.DevOffsetChanged -= OnDevOffsetChanged;
            }
            _plotsPresenter?.Dispose();
            _pointer?.Dispose();
            _healPresenter?.Dispose();
            _deliveryViews?.Dispose();
            _delivery?.Dispose();
            _zones?.Dispose();
            _treePresenter?.Dispose();
            _waveAnnouncer?.Dispose();
            _eventAnnouncer?.Dispose();
            _miniEvents?.Dispose();
            _dailyWave?.Dispose();
            _quiz?.Dispose();
            _vibrancy?.Dispose();
        }
    }
}
