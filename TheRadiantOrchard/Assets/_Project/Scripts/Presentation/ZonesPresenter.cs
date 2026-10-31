using System;
using System.Collections.Generic;
using RadiantOrchard.Core;
using RadiantOrchard.Gameplay;
using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Shows the farm's five zones grey or in colour by vibrancy stage (CLAUDE.md 5.1, 5.7): zone N is in colour from
    /// stage N. A stage reached in play sweeps colour up over its zone; the full unlock sequence (camera glide,
    /// particles, sting, banner) builds on this in W2·11. Zones above the stage go grey at once (dev stage down).
    /// Dispose to unsubscribe.
    /// </summary>
    public sealed class ZonesPresenter : IDisposable
    {
        private readonly IReadOnlyList<Desaturation> _zones;
        private readonly Bounds[] _bounds;
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

        /// <summary>Zone (0-based) is in colour or on its way there.</summary>
        public bool IsColoured(int zone) => _zones[zone].IsSweeping || _zones[zone].Saturation >= 1f;

        private void OnStageReached(StageReached e)
        {
            int z = e.Stage - 1;
            if (z < 0 || z >= _zones.Count || _zones[z] == null) return;
            Bounds b = _bounds[z];
            _zones[z].Sweep(new Vector2(b.center.x, b.min.y), Vector2.up, b.size.y, _sweepSeconds);
        }

        private void OnVibrancyChanged(VibrancyChanged e)
        {
            for (int z = 0; z < _zones.Count; z++)
            {
                Desaturation zone = _zones[z];
                if (zone == null) continue;
                bool coloured = z < e.Stage;
                if (!coloured && IsColoured(z)) zone.SetSaturation(0f);
                else if (coloured && !IsColoured(z)) zone.SetSaturation(1f);
            }
        }
    }
}
