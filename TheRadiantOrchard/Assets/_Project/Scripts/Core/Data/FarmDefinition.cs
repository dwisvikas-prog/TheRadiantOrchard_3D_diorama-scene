using System.Collections.Generic;
using UnityEngine;

namespace RadiantOrchard.Core
{
    /// <summary>
    /// The farm layout in isometric grid cells (cell 0,0 is the farm centre). Data, not scene wiring, so a different
    /// farm (Phase 2 worlds) is a new asset. The editor's farm builder lays the Game scene out from this.
    /// </summary>
    [CreateAssetMenu(menuName = "Radiant Orchard/Farm Definition", fileName = "FarmDefinition")]
    public sealed class FarmDefinition : ScriptableObject
    {
        [Header("Farm")]
        [Tooltip("The whole farm clearing, in cells. Zones must cover it exactly.")]
        [SerializeField] private RectInt _farmCells = new RectInt(-12, -12, 24, 24);
        [Tooltip("Extra world units around the farm the camera may show (jungle border).")]
        [Min(0f)] [SerializeField] private float _cameraMarginWorld = 2f;

        [Header("Plots (5.3), in unlock order; first ones are open at the start")]
        [SerializeField] private Vector2Int _plotSizeCells = new Vector2Int(3, 3);
        [Tooltip("Bottom corner cell of each plot. Order = unlock order (BalanceConfig.PlotsOpenAtStage).")]
        [SerializeField] private Vector2Int[] _plotCells =
        {
            new Vector2Int(-5, -1), new Vector2Int(-1, -1), new Vector2Int(3, -1),
            new Vector2Int(-5, 3), new Vector2Int(-1, 3),
            new Vector2Int(3, 3), new Vector2Int(-5, -5),
            new Vector2Int(-1, -5), new Vector2Int(3, -5),
        };

        [Header("Landmarks (5.1)")]
        [SerializeField] private Vector2Int _wisdomTreeCell = new Vector2Int(-1, 8);
        [SerializeField] private Vector2Int _wellCell = new Vector2Int(8, 1);
        [SerializeField] private Vector2Int _benchCell = new Vector2Int(6, -11);

        [Header("Visitor path (5.4)")]
        [Tooltip("Where visitors appear, outside the farm in the jungle.")]
        [SerializeField] private Vector2Int _spawnCell = new Vector2Int(-14, -9);
        [SerializeField] private Vector2Int[] _waitingSpotCells = { new Vector2Int(-3, -9), new Vector2Int(0, -9), new Vector2Int(3, -9) };
        [SerializeField] private Vector2Int _exitCell = new Vector2Int(13, -9);

        [Header("Zones (5.1, 5.7): zone 1 turns colour at stage 1 ... zone 5 at stage 5")]
        [SerializeField] private RectInt[] _zoneCells =
        {
            new RectInt(-6, -6, 12, 12),
            new RectInt(-12, -12, 6, 24),
            new RectInt(6, -12, 6, 24),
            new RectInt(-6, 6, 12, 6),
            new RectInt(-6, -12, 12, 6),
        };

        public RectInt FarmCells => _farmCells;
        public float CameraMarginWorld => _cameraMarginWorld;
        public Vector2Int PlotSizeCells => _plotSizeCells;
        public IReadOnlyList<Vector2Int> PlotCells => _plotCells;
        public Vector2Int WisdomTreeCell => _wisdomTreeCell;
        public Vector2Int WellCell => _wellCell;
        public Vector2Int BenchCell => _benchCell;
        public Vector2Int SpawnCell => _spawnCell;
        public IReadOnlyList<Vector2Int> WaitingSpotCells => _waitingSpotCells;
        public Vector2Int ExitCell => _exitCell;
        public IReadOnlyList<RectInt> ZoneCells => _zoneCells;

        /// <summary>The cells a plot covers.</summary>
        public RectInt PlotRect(int index) => new RectInt(_plotCells[index], _plotSizeCells);

        private void OnValidate()
        {
            if (_zoneCells == null || _zoneCells.Length != BalanceConfig.StageCount)
                Debug.LogError($"{name}: needs exactly {BalanceConfig.StageCount} zones.", this);
            if (_plotCells == null || _plotCells.Length == 0)
                Debug.LogError($"{name}: has no plots.", this);
        }
    }
}
