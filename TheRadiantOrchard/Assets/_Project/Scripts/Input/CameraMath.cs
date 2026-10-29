using UnityEngine;

namespace RadiantOrchard.Input
{
    /// <summary>Pure camera maths for a 2D orthographic camera: zoom limits, bounds clamping, zooming around a point.</summary>
    public static class CameraMath
    {
        /// <summary>
        /// Clamps the orthographic size to [min, max], and never larger than what the bounds can fill at this aspect,
        /// so zooming out never shows past the edge of the farm.
        /// </summary>
        public static float ClampOrthoSize(float size, float min, float max, Rect bounds, float aspect)
        {
            float fit = Mathf.Min(bounds.height * 0.5f, bounds.width / (2f * aspect));
            float upper = Mathf.Max(min, Mathf.Min(max, fit));
            return Mathf.Clamp(size, min, upper);
        }

        /// <summary>
        /// Keeps the view inside the bounds. On an axis where the view is larger than the bounds, it centres instead.
        /// </summary>
        public static Vector2 ClampPosition(Vector2 position, float orthoSize, float aspect, Rect bounds)
        {
            float halfHeight = orthoSize;
            float halfWidth = orthoSize * aspect;
            return new Vector2(
                ClampAxis(position.x, halfWidth, bounds.xMin, bounds.xMax),
                ClampAxis(position.y, halfHeight, bounds.yMin, bounds.yMax));
        }

        /// <summary>New camera position after a zoom so that <paramref name="focus"/> stays at the same spot on screen.</summary>
        public static Vector2 ZoomAround(Vector2 cameraPosition, Vector2 focus, float oldSize, float newSize)
        {
            if (oldSize <= 0f) return cameraPosition;
            return focus + (cameraPosition - focus) * (newSize / oldSize);
        }

        /// <summary>World units per screen pixel for an orthographic camera.</summary>
        public static float WorldPerPixel(float orthoSize, float screenHeight) =>
            screenHeight > 0f ? 2f * orthoSize / screenHeight : 0f;

        /// <summary>World position under a screen point for an orthographic camera centred at <paramref name="cameraPosition"/>.</summary>
        public static Vector2 ScreenToWorld(Vector2 screen, Vector2 cameraPosition, float orthoSize, float screenWidth, float screenHeight)
        {
            float perPixel = WorldPerPixel(orthoSize, screenHeight);
            return cameraPosition + new Vector2((screen.x - screenWidth * 0.5f) * perPixel, (screen.y - screenHeight * 0.5f) * perPixel);
        }

        private static float ClampAxis(float value, float half, float min, float max)
        {
            if (max - min <= 2f * half) return (min + max) * 0.5f;
            return Mathf.Clamp(value, min + half, max - half);
        }
    }
}
