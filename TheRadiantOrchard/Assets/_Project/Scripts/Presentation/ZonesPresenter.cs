using System;
using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows the farm's five zones grey or in colour by vibrancy stage (CLAUDE.md 5.1, 5.7): zone N is in colour from
    /// stage N. A zone whose stage is reached in play stays grey until the unlock sequence sweeps it (Sweep), so the
    /// colour arrives while the camera is looking at it. Zones above the stage go grey at once (dev stage down).
    /// Dispose to unsubscribe.
    /// </summary>
    public sealed class ZonesPresenter : IDisposable
    {
        private readonly IReadOnlyList<Desaturation> _zones;
        private readonly Bounds[] _bounds;
        private readonly bool[] _pending;
        private readonly EventBus _events;
        private readonly float _sweepSeconds;

        public ZonesPresenter(FarmView farm, GameState state, EventBus events)
        {
            if (farm == null) throw new ArgumentNullException(nameof(farm));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _zones = farm.ZoneColours;
            _sweepSeconds = farm.ZoneSweepSeconds;
            if (_zones.Count != BalanceConfig.StageCount || farm.Zones.Count != _zones.Count)
                Log.Error($"ZonesPresenter: {farm.Zones.Count} zone tilemaps and {_zones.Count} zone colours for {BalanceConfig.StageCount} stages; rebuild the farm.");

            // Renderer bounds once here, not per sweep.
            _bounds = new Bounds[_zones.Count];
            _pending = new bool[_zones.Count];
            for (int z = 0; z < _bounds.Length && z < farm.Zones.Count; z++)
                if (farm.Zones[z] != null && farm.Zones[z].TryGetComponent(out Renderer renderer)) _bounds[z] = renderer.bounds;

            for (int z = 0; z < _zones.Count; z++)
                if (_zones[z] != null) _zones[z].SetSaturation(z < state.VibrancyStage ? 1f : 0f);

            _events.Subscribe<StageReached>(OnStageReached);
            _events.Subscribe<VibrancyChanged>(OnVibrancyChanged);
        }

        public void Dispose()
        {
            _events.Unsubscribe<StageReached>(OnStageReached);
            _events.Unsubscribe<VibrancyChanged>(OnVibrancyChanged);
        }

        public int Count => _zones.Count;
        public float SweepSeconds => _sweepSeconds;

        /// <summary>World area of a zone (0-based) on screen.</summary>
        public Rect AreaOf(int zone)
        {
            Bounds b = _bounds[zone];
            return new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
        }

        /// <summary>Zone (0-based) is in colour or on its way there.</summary>
        public bool IsColoured(int zone) => _zones[zone].IsSweeping || _zones[zone].Saturation >= 1f;

        /// <summary>Its stage was reached and it is waiting for the unlock sequence to colour it.</summary>
        public bool IsPending(int zone) => _pending[zone];

        /// <summary>Washes colour up over the zone (0-based), bottom to top, in SweepSeconds.</summary>
        public void Sweep(int zone)
        {
            if (zone < 0 || zone >= _zones.Count || _zones[zone] == null) return;
            _pending[zone] = false;
            Bounds b = _bounds[zone];
            _zones[zone].Sweep(new Vector2(b.center.x, b.min.y), Vector2.up, b.size.y, _sweepSeconds);
        }

        private void OnStageReached(StageReached e)
        {
            int z = e.Stage - 1;
            if (z >= 0 && z < _zones.Count) _pending[z] = true;
        }

        private void OnVibrancyChanged(VibrancyChanged e)
        {
            for (int z = 0; z < _zones.Count; z++)
            {
                Desaturation zone = _zones[z];
                if (zone == null) continue;
                bool coloured = z < e.Stage;
                if (!coloured)
                {
                    _pending[z] = false;
                    if (IsColoured(z)) zone.SetSaturation(0f);
                }
                else if (!_pending[z] && !IsColoured(z)) zone.SetSaturation(1f);
            }
        }
    }
}
