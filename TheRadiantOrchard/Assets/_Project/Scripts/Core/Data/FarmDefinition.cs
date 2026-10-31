using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// The farm layout in isometric grid cells (cell 0,0 is the farm centre). Data, not scene wiring, so a different
    /// farm (Phase 2 worlds) is a new asset. The editor's farm builder lays the Game scene out from this.
    /// Layout v2 (Vishal's concept, 31 Oct): two crossing path bands split the clearing into four areas around a centre
    /// square. On screen: x &lt; 0, y ≥ 0 is the left area, x ≥ 0, y &lt; 0 the right, both negative the bottom, both
    /// positive the top. Visitors come out of the jungle along the lower-left path, wait in the centre square and leave
    /// along the lower-right path.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Farm Definition", fileName = "FarmDefinition")]
    public sealed class FarmDefinition : ScriptableObject
    {
        [Header("Farm")]
        [Tooltip("The whole farm clearing, in cells. Zones must cover it exactly.")]
        [SerializeField] private RectInt _farmCells = new RectInt(-9, -9, 18, 18);
        [Tooltip("Extra world units around the farm the camera may show (jungle border).")]
        [Min(0f)] [SerializeField] private float _cameraMarginWorld = 4f;

        [Header("Plots (5.3), in unlock order; first ones are open at the start")]
        [SerializeField] private Vector2Int _plotSizeCells = new Vector2Int(3, 3);
        [Tooltip("Bottom corner cell of each plot. Order = unlock order (BalanceConfig.PlotsOpenAtStage).")]
        [SerializeField] private Vector2Int[] _plotCells =
        {
            // Three per area with a one-cell gap between fences. Left area (zone 1): open at the start.
            new Vector2Int(-8, 5), new Vector2Int(-4, 5), new Vector2Int(-8, 1),
            // Right area (zone 2): stage 2.
            new Vector2Int(1, -8), new Vector2Int(5, -8),
            // Bottom area (zone 3): stage 3.
            new Vector2Int(-8, -8), new Vector2Int(-4, -8),
            // Stage 4: the third plot of the right and bottom areas.
            new Vector2Int(5, -4), new Vector2Int(-8, -4),
        };

        [Header("Landmarks (5.1)")]
        [SerializeField] private Vector2Int _wisdomTreeCell = new Vector2Int(4, 4);
        [SerializeField] private Vector2Int _wellCell = new Vector2Int(-4, 2);
        [SerializeField] private Vector2Int _benchCell = new Vector2Int(1, 1);

        [Header("Paths and visitors (5.4)")]
        [Tooltip("Path bands (drawn on top of the zone ground), in cells; may reach into the jungle.")]
        [SerializeField] private RectInt[] _pathCells =
        {
            new RectInt(-13, -1, 10, 2), // lower-left: from the jungle to the centre square
            new RectInt(-1, -13, 2, 10), // lower-right: from the centre square back to the jungle
            new RectInt(3, -1, 6, 2),    // upper-right
            new RectInt(-1, 3, 2, 6),    // upper-left
        };
        [Tooltip("Walk in, in cell coordinates: from the spawn point in the jungle to the edge of the centre square.")]
        [SerializeField] private Vector2[] _entryRoute = { new Vector2(-13f, 0f), new Vector2(-3f, 0f) };
        [Tooltip("Walk out, in cell coordinates: from the edge of the centre square to the exit in the jungle.")]
        [SerializeField] private Vector2[] _exitRoute = { new Vector2(0f, -3f), new Vector2(0f, -13f) };
        [SerializeField] private Vector2Int[] _waitingSpotCells = { new Vector2Int(-2, 0), new Vector2Int(-1, -1), new Vector2Int(0, -2) };

        [Header("Zones (5.1, 5.7): zone 1 turns colour at stage 1 ... zone 5 at stage 5")]
        [SerializeField] private ZoneArea[] _zones =
        {
            new ZoneArea("Left plot area", new RectInt(-9, 3, 9, 6), new RectInt(-9, 0, 6, 3)),
            new ZoneArea("Right plot area", new RectInt(0, -9, 9, 6), new RectInt(3, -3, 6, 3)),
            new ZoneArea("Bottom plot area", new RectInt(-9, -9, 9, 6), new RectInt(-9, -3, 6, 3)),
            new ZoneArea("Centre waiting square", new RectInt(-3, -3, 6, 6)),
            new ZoneArea("Wisdom Tree area", new RectInt(0, 3, 9, 6), new RectInt(3, 0, 6, 3)),
        };

        public RectInt FarmCells => _farmCells;
        public float CameraMarginWorld => _cameraMarginWorld;
        public Vector2Int PlotSizeCells => _plotSizeCells;
        public IReadOnlyList<Vector2Int> PlotCells => _plotCells;
        public Vector2Int WisdomTreeCell => _wisdomTreeCell;
        public Vector2Int WellCell => _wellCell;
        public Vector2Int BenchCell => _benchCell;
        public IReadOnlyList<RectInt> PathCells => _pathCells;
        public IReadOnlyList<Vector2> EntryRoute => _entryRoute;
        public IReadOnlyList<Vector2> ExitRoute => _exitRoute;
        /// <summary>Where visitors appear (the start of the entry route), in cell coordinates.</summary>
        public Vector2 SpawnPoint => _entryRoute[0];
        /// <summary>Where visitors leave (the end of the exit route), in cell coordinates.</summary>
        public Vector2 ExitPoint => _exitRoute[_exitRoute.Length - 1];
        public IReadOnlyList<Vector2Int> WaitingSpotCells => _waitingSpotCells;
        public IReadOnlyList<ZoneArea> Zones => _zones;

        /// <summary>The cells a plot covers.</summary>
        public RectInt PlotRect(int index) => new RectInt(_plotCells[index], _plotSizeCells);

        /// <summary>Which plot covers this cell, or -1 if none (used to turn a tap into a plot).</summary>
        public int PlotIndexAtCell(Vector2Int cell)
        {
            for (int i = 0; i < _plotCells.Length; i++)
                if (PlotRect(i).Contains(cell)) return i;
            return -1;
        }

        /// <summary>Which zone (0-based) holds this cell, or -1 outside the farm.</summary>
        public int ZoneIndexAtCell(Vector2Int cell)
        {
            for (int i = 0; i < _zones.Length; i++)
                if (_zones[i] != null && _zones[i].Contains(cell)) return i;
            return -1;
        }

        public bool IsPathCell(Vector2Int cell)
        {
            for (int i = 0; i < _pathCells.Length; i++)
                if (_pathCells[i].Contains(cell)) return true;
            return false;
        }

        private void OnValidate()
        {
            if (_zones == null || _zones.Length != BalanceConfig.StageCount)
                Debug.LogError($"{name}: needs exactly {BalanceConfig.StageCount} zones.", this);
            if (_plotCells == null || _plotCells.Length == 0)
                Debug.LogError($"{name}: has no plots.", this);
            if (_entryRoute == null || _entryRoute.Length < 2 || _exitRoute == null || _exitRoute.Length < 2)
                Debug.LogError($"{name}: the entry and exit routes need at least two points each.", this);
        }
    }
}
