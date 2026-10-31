using System;
using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// One farm zone (CLAUDE.md 5.1) as one or more cell rectangles, so an area can be L-shaped (a quarter of the farm
    /// minus the centre square). Data only.
    /// </summary>
    [Serializable]
    public sealed class ZoneArea
    {
        [Tooltip("Shown in the editor only, e.g. 'Left plot area'.")]
        [SerializeField] private string _name;
        [SerializeField] private RectInt[] _rects = new RectInt[0];

        public ZoneArea() { }

        public ZoneArea(string name, params RectInt[] rects)
        {
            _name = name;
            _rects = rects;
        }

        public string Name => _name;
        public IReadOnlyList<RectInt> Rects => _rects;

        public bool Contains(Vector2Int cell)
        {
            for (int i = 0; i < _rects.Length; i++)
                if (_rects[i].Contains(cell)) return true;
            return false;
        }

        /// <summary>Every cell of the zone (rectangles must not overlap).</summary>
        public IEnumerable<Vector2Int> Cells()
        {
            for (int i = 0; i < _rects.Length; i++)
                foreach (Vector2Int cell in _rects[i].allPositionsWithin)
                    yield return cell;
        }
    }
}
