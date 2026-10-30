using UnityEngine;

namespace RadiantOrchard.Input
{
    /// <summary>
    /// The screen points a swipe passed through, in order. A view over GestureRules' reused buffer: read it inside
    /// the Swiped handler, do not keep it.
    /// </summary>
    public readonly struct SwipePath
    {
        private readonly Vector2[] _points;

        public SwipePath(Vector2[] points, int count)
        {
            _points = points;
            Count = count;
        }

        public int Count { get; }

        public Vector2 this[int index] => _points[index];

        /// <summary>
        /// How many equal steps to split the segment a→b into so no step is longer than spacing. A fast swipe can
        /// move far between two frames; testing every step keeps it from jumping over a tree.
        /// </summary>
        public static int Steps(Vector2 a, Vector2 b, float spacing) =>
            spacing <= 0f ? 1 : Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / spacing));
    }
}
