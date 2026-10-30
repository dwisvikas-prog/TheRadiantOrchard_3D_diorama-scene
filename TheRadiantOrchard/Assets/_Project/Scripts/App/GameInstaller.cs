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

        private AppServices _services;
        private PlotManager _plots;
        private PlotsPresenter _plotsPresenter;
        private FarmPointerHandler _pointer;
        private HealService _heal;
        private HealPresenter _healPresenter;
        private VisitorManager _visitors;
        private DeliveryService _delivery;
        private HarvestDelivery _deliveryViews;

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
                || _visitorViews == null || _popups == null || _hud == null || _basket == null)
            {
                Log.Error("GameInstaller: Farm View, Camera Controller, Gestures, Plant Menu, Tonics, Visitor Views, Popups, HUD and Basket must be assigned (Radiant Orchard > Setup).", this);
                return;
            }

            _services = services;
            _cameraController.SetBounds(_farmView.WorldBounds);
            _cameraController.Init(services.Config.Balance);

            _plots = new PlotManager(services.State, services.Config, services.Time, services.Events, services.Save.MarkDirty);
            _plots.ApplyStageUnlocks();
            _plotsPresenter = new PlotsPresenter(_farmView.Plots, _plots, services.Config, services.Events);
            services.Clock.Ticked += _plots.Tick;
            services.Clock.Ticked += _plotsPresenter.Tick;
            _heal = new HealService(services.State, services.Config, services.Events, services.Save.MarkDirty);
            _visitors = new VisitorManager(services.State, services.Config, services.Time, services.Events, _heal, services.Save.MarkDirty, new System.Random());
            services.Clock.Ticked += TickVisitors;
            _delivery = new DeliveryService(services.State, services.Config, services.Events, _visitors, services.Save.MarkDirty);
            _plots.HarvestGate = _delivery.CanTake;
            // After the visitors' tick, so a visitor who just reached their spot gets basket fruit the same tick.
            services.Clock.Ticked += _delivery.Tick;
            _visitorViews.Init(_visitors, services.Config, services.Events, _farmView);
            _healPresenter = new HealPresenter(_visitorViews, _popups, services.Config, services.Events);
            _hud.Init(services.State, services.Events, services.Config.Strings);
            _basket.Init(services.Config, services.State, services.Events);
            _deliveryViews = new HarvestDelivery(services.Events, _delivery, _visitorViews, _tonics, _popups, _farmView, _basket,
                _cameraController.GetComponent<Camera>(), services.Config);
            // Visitors who came while the app was closed (or in the background) are waiting once the views are ready.
            _visitors.ArriveAfterAway(services.LaunchTimeAway);
            services.Events.Subscribe<TimeAwayApplied>(OnTimeAway);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            DevGame.Current = new DevGame(services.Config, services.State, services.Time, services.Events, services.Save, _plots, _heal, _visitors, _delivery);
#endif

            _gestures.Init(services.Config.Balance, _cameraController);
            _plantMenu.Init(_plots, services.Config, services.State, services.Events);
            _pointer = new FarmPointerHandler(_gestures, _cameraController.GetComponent<Camera>(), _farmView, services.Config.Farm,
                _plots, _plantMenu, services.Config.Balance.SwipeSampleSpacing);

            Log.Info($"GameInstaller: ready; {services.State.Seeds} seeds, {services.State.HealedTotal} healed, away {services.LaunchTimeAway}.");
        }

        private void TickVisitors() => _visitors.Tick(_services.Clock.IntervalSeconds);

        private void OnTimeAway(TimeAwayApplied e) => _visitors.ArriveAfterAway(e.Elapsed);

        private void OnDestroy()
        {
            if (_services == null) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (DevGame.Current != null && DevGame.Current.Plots == _plots) DevGame.Current = null;
#endif
            if (_plots != null) _services.Clock.Ticked -= _plots.Tick;
            if (_plotsPresenter != null) _services.Clock.Ticked -= _plotsPresenter.Tick;
            if (_delivery != null) _services.Clock.Ticked -= _delivery.Tick;
            if (_visitors != null)
            {
                _services.Clock.Ticked -= TickVisitors;
                _services.Events.Unsubscribe<TimeAwayApplied>(OnTimeAway);
            }
            _plotsPresenter?.Dispose();
            _pointer?.Dispose();
            _healPresenter?.Dispose();
            _deliveryViews?.Dispose();
            _delivery?.Dispose();
        }
    }
}
