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

        private AppServices _services;
        private PlotManager _plots;
        private PlotsPresenter _plotsPresenter;
        private FarmPointerHandler _pointer;

        private void Awake()
        {
            AppServices services = AppServices.Current;
            if (services == null)
            {
                // Play was pressed with Game open: boot first, so every session starts the same way.
                SceneManager.LoadScene(_bootSceneName);
                return;
            }

            if (_farmView == null || _cameraController == null || _gestures == null || _plantMenu == null || _tonics == null)
            {
                Log.Error("GameInstaller: Farm View, Camera Controller, Gestures, Plant Menu and Tonics must be assigned (Radiant Orchard > Setup).", this);
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
            _tonics.Init(services.Events, services.Config, _farmView);

            _gestures.Init(services.Config.Balance, _cameraController);
            _plantMenu.Init(_plots, services.Config, services.State, services.Events);
            _pointer = new FarmPointerHandler(_gestures, _cameraController.GetComponent<Camera>(), _farmView, services.Config.Farm,
                _plots, _plantMenu, services.Config.Balance.SwipeSampleSpacing);

            Log.Info($"GameInstaller: ready; {services.State.Seeds} seeds, {services.State.HealedTotal} healed, away {services.LaunchTimeAway}.");
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
        }
    }
}
