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
        [SerializeField] private PlaceholderVisitor _visitor;
        [SerializeField] private VirtuePopups _popups;
        [SerializeField] private HudView _hud;

        private AppServices _services;
        private PlotManager _plots;
        private PlotsPresenter _plotsPresenter;
        private FarmPointerHandler _pointer;
        private HealService _heal;
        private HealPresenter _healPresenter;

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
                || _visitor == null || _popups == null || _hud == null)
            {
                Log.Error("GameInstaller: Farm View, Camera Controller, Gestures, Plant Menu, Tonics, Visitor, Popups and HUD must be assigned (Radiant Orchard > Setup).", this);
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
            _visitor.Init(services.Config);
            _healPresenter = new HealPresenter(_visitor, _popups, services.Config, services.Events);
            _tonics.Target = _visitor.transform;
            _tonics.Init(services.Events, services.Config, _farmView);
            _tonics.Arrived += OnTonicArrived;
            _hud.Init(services.State, services.Events, services.Config.Strings);

            _gestures.Init(services.Config.Balance, _cameraController);
            _plantMenu.Init(_plots, services.Config, services.State, services.Events);
            _pointer = new FarmPointerHandler(_gestures, _cameraController.GetComponent<Camera>(), _farmView, services.Config.Farm,
                _plots, _plantMenu, services.Config.Balance.SwipeSampleSpacing);

            Log.Info($"GameInstaller: ready; {services.State.Seeds} seeds, {services.State.HealedTotal} healed, away {services.LaunchTimeAway}.");
        }

        // Until the Week 2 basket (CLAUDE.md 5.5), a tonic the visitor did not ask for just pops at the spot.
        private void OnTonicArrived(string fruitId, Vector2 point)
        {
            if (HealRules.Accepts(_visitor.RequestedFruitId, fruitId)) _heal.Heal(fruitId);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>For dev tools only (Radiant Orchard > Dev menu, later the dev panel).</summary>
        public PlotManager DevPlots => _plots;
#endif

        private void OnDestroy()
        {
            if (_services == null) return;
            if (_plots != null) _services.Clock.Ticked -= _plots.Tick;
            if (_plotsPresenter != null) _services.Clock.Ticked -= _plotsPresenter.Tick;
            _plotsPresenter?.Dispose();
            _pointer?.Dispose();
            _healPresenter?.Dispose();
            if (_tonics != null) _tonics.Arrived -= OnTonicArrived;
        }
    }
}
