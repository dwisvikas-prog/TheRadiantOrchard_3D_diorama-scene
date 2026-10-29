using System;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using RadiantOrchard.Input;
using RadiantOrchard.Presentation;
using RadiantOrchard.UI;
using UnityEngine;

namespace RadiantOrchard.App
{
    /// <summary>
    /// Turns a tap on the farm into an action: an open, empty plot opens the plant menu. Lives in the composition
    /// root because it joins Input (taps) to UI (menu), which may not reference each other. Dispose to unsubscribe.
    /// </summary>
    public sealed class PlotTapHandler : IDisposable
    {
        private readonly TapDetector _taps;
        private readonly Camera _camera;
        private readonly FarmView _farmView;
        private readonly FarmDefinition _farm;
        private readonly PlotManager _plots;
        private readonly PlantMenuView _plantMenu;

        public PlotTapHandler(TapDetector taps, Camera camera, FarmView farmView, FarmDefinition farm, PlotManager plots, PlantMenuView plantMenu)
        {
            _taps = taps;
            _camera = camera;
            _farmView = farmView;
            _farm = farm;
            _plots = plots;
            _plantMenu = plantMenu;
            _taps.Tapped += OnTapped;
        }

        public void Dispose() => _taps.Tapped -= OnTapped;

        private void OnTapped(Vector2 screen)
        {
            if (_plantMenu.IsOpen) return;

            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            Vector3Int cell = _farmView.Grid.WorldToCell(new Vector3(world.x, world.y, 0f));
            int plot = _farm.PlotIndexAtCell(new Vector2Int(cell.x, cell.y));
            if (plot < 0) return;

            PlotInfo info = _plots.Get(plot);
            if (info.Unlocked && info.IsEmpty) _plantMenu.Show(plot);
        }
    }
}
