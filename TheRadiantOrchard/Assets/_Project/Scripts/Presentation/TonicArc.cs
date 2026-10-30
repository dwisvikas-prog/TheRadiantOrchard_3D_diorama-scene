using UnityEngine;

namespace RadiantOrchard.Presentation
{
    /// <summary>
    /// The tonic's flight curve (salvaged from the old TonicFlight, adapted to 2D): a quadratic Bezier from the tree
    /// to the target whose halfway point sits apexHeight above the straight line's midpoint, nudged sideways across
    /// the flight line so several tonics in the air never trace the same path. Pure maths; no allocations.
    /// </summary>
    public static class TonicArc
    {
        /// <summary>
        /// Control point for the curve. A quadratic Bezier only reaches half of its control point's offset at t = 0.5,
        /// so the offsets are doubled to put the halfway point exactly apexHeight up and sideways across.
        /// </summary>
        public static Vector2 ControlPoint(Vector2 start, Vector2 end, float apexHeight, float sideways)
        {
            Vector2 mid = (start + end) * 0.5f;
            Vector2 along = end - start;
            Vector2 across = along.sqrMagnitude > 1e-6f ? new Vector2(-along.y, along.x).normalized : Vector2.zero;
            return mid + Vector2.up * (2f * apexHeight) + across * (2f * sideways);
        }

        /// <summary>Point on the curve at t (0 = start, 1 = end; clamped).</summary>
        public static Vector2 Evaluate(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }
    }
}
