using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// Pure maths for walking a figure along a polyline (a visitor following the farm paths): the point a fraction of
    /// the way along its length, so the pace is even on long and short legs. Allocation-free.
    /// </summary>
    public static class RouteMath
    {
        /// <summary>The point t (0..1, clamped) of the way along the first count points; the first point when count is 1.</summary>
        public static Vector2 PointAt(Vector2[] points, int count, float t)
        {
            if (points == null || count <= 0) return Vector2.zero;
            if (count == 1) return points[0];

            float total = 0f;
            for (int i = 1; i < count; i++) total += Vector2.Distance(points[i - 1], points[i]);
            if (total <= 0f) return points[count - 1];

            float target = Mathf.Clamp01(t) * total;
            for (int i = 1; i < count; i++)
            {
                float leg = Vector2.Distance(points[i - 1], points[i]);
                if (target <= leg && leg > 0f) return Vector2.Lerp(points[i - 1], points[i], target / leg);
                target -= leg;
            }
            return points[count - 1];
        }
    }
}
