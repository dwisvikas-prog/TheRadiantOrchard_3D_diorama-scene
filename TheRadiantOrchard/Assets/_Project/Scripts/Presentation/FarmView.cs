using System.Collections.Generic;
using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Scene references for the farm, laid out from FarmDefinition by the editor's farm builder. Systems get what they
    /// need from here through the GameInstaller; nothing searches the scene at runtime.
    /// </summary>
    public sealed class FarmView : MonoBehaviour
    {
        [SerializeField] private Grid _grid;
        [Tooltip("Plot roots in unlock order (same order as FarmDefinition.PlotCells).")]
        [SerializeField] private Transform[] _plots = new Transform[0];
        [SerializeField] private Transform _wisdomTree;
        [SerializeField] private Transform _well;
        [SerializeField] private Transform _bench;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private Transform[] _waitingSpots = new Transform[0];
        [SerializeField] private Transform _exitPoint;
        [Tooltip("One tilemap per zone, zone 1 first.")]
        [SerializeField] private Tilemap[] _zones = new Tilemap[0];
        [Tooltip("World area the camera may show: the farm plus its margin.")]
        [SerializeField] private Rect _worldBounds;

        public Grid Grid => _grid;
        public IReadOnlyList<Transform> Plots => _plots;
        public Transform WisdomTree => _wisdomTree;
        public Transform Well => _well;
        public Transform Bench => _bench;
        public Transform SpawnPoint => _spawnPoint;
        public IReadOnlyList<Transform> WaitingSpots => _waitingSpots;
        public Transform ExitPoint => _exitPoint;
        public IReadOnlyList<Tilemap> Zones => _zones;
        public Rect WorldBounds => _worldBounds;

        private void Awake()
        {
            if (_grid == null || _wisdomTree == null || _well == null || _bench == null || _spawnPoint == null || _exitPoint == null)
                Log.Error($"FarmView '{name}': a scene reference is missing; rebuild the farm (Radiant Orchard > Setup > Build Placeholder Farm).", this);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 1f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(_worldBounds.center, _worldBounds.size);
        }
    }
}
