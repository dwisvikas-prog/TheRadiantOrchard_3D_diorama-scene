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
    /// Turns gestures on the farm into actions. A tap on the Wisdom Tree while a quiz waits opens the quiz; a tap on an
    /// open, empty plot opens the plant menu; taps and harvest gestures on planted plots go to PlotManager, which judges
    /// them (HarvestRules). A swipe acts on every plot its path crosses. Lives in the composition root because it joins Input to UI and Gameplay, which may not
    /// reference each other. Dispose to unsubscribe.
    /// </summary>
    public sealed class FarmPointerHandler : IDisposable
    {
        private readonly GestureRecognizer _gestures;
        private readonly Camera _camera;
        private readonly FarmView _farmView;
        private readonly FarmDefinition _farm;
        private readonly PlotManager _plots;
        private readonly PlantMenuView _plantMenu;
        private readonly QuizManager _quiz;
        private readonly QuizPanelView _quizPanel;
        private readonly float _swipeSampleSpacing;

        public FarmPointerHandler(GestureRecognizer gestures, Camera camera, FarmView farmView, FarmDefinition farm,
            PlotManager plots, PlantMenuView plantMenu, QuizManager quiz, QuizPanelView quizPanel, float swipeSampleSpacingFraction)
        {
            _gestures = gestures;
            _camera = camera;
            _farmView = farmView;
            _farm = farm;
            _plots = plots;
            _plantMenu = plantMenu;
            _quiz = quiz;
            _quizPanel = quizPanel;
            _swipeSampleSpacing = swipeSampleSpacingFraction;

            _gestures.Tapped += OnTapped;
            _gestures.Gestured += OnGestured;
            _gestures.Swiped += OnSwiped;
        }

        public void Dispose()
        {
            _gestures.Tapped -= OnTapped;
            _gestures.Gestured -= OnGestured;
            _gestures.Swiped -= OnSwiped;
        }

        private bool PanelOpen => _plantMenu.IsOpen || _quizPanel.IsOpen;

        private void OnTapped(Vector2 screen)
        {
            if (PanelOpen) return;
            if (_quiz.IsReady && _farmView.WisdomTreeView != null && _farmView.WisdomTreeView.Contains(ToWorld(screen)))
            {
                _quizPanel.Show();
                return;
            }
            int plot = PlotAt(screen);
            if (plot < 0) return;

            PlotInfo info = _plots.Get(plot);
            if (info.Unlocked && info.IsEmpty) _plantMenu.Show(plot);
            else _plots.HandleTap(plot);
        }

        private void OnGestured(GestureType gesture, Vector2 screen)
        {
            if (PanelOpen) return;
            int plot = PlotAt(screen);
            if (plot >= 0) _plots.HandleGesture(plot, gesture);
        }

        // Every plot the swipe passes over is judged once, so one swipe can pick a row of ripe swipe-fruit trees.
        private void OnSwiped(SwipePath path)
        {
            if (PanelOpen || path.Count == 0) return;
            float spacing = _swipeSampleSpacing * Screen.height;
            int crossed = 0;

            Vector2 previous = path[0];
            crossed |= Bit(PlotAt(previous));
            for (int i = 1; i < path.Count; i++)
            {
                Vector2 next = path[i];
                int steps = SwipePath.Steps(previous, next, spacing);
                for (int s = 1; s <= steps; s++)
                    crossed |= Bit(PlotAt(Vector2.Lerp(previous, next, (float)s / steps)));
                previous = next;
            }

            for (int plot = 0; plot < _plots.Count; plot++)
                if ((crossed & (1 << plot)) != 0) _plots.HandleGesture(plot, GestureType.Swipe);
        }

        private static int Bit(int plot) => plot >= 0 && plot < 32 ? 1 << plot : 0;

        // The tree in front wins (lowest on screen draws on top); otherwise the plot whose ground cells were hit.
        private Vector2 ToWorld(Vector2 screen)
        {
            Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));
            return new Vector2(world.x, world.y);
        }

        private int PlotAt(Vector2 screen)
        {
            Vector2 world = ToWorld(screen);

            int best = -1;
            float bestY = float.MaxValue;
            var views = _farmView.Plots;
            for (int i = 0; i < views.Count; i++)
            {
                PlotView view = views[i];
                if (view == null || !view.TreeContains(world)) continue;
                float y = view.transform.position.y;
                if (y < bestY)
                {
                    bestY = y;
                    best = i;
                }
            }
            if (best >= 0) return best;

            Vector3Int cell = _farmView.Grid.WorldToCell(new Vector3(world.x, world.y, 0f));
            return _farm.PlotIndexAtCell(new Vector2Int(cell.x, cell.y));
        }
    }
}
