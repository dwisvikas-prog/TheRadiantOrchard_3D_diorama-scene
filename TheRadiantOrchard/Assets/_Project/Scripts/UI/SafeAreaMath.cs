using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>Pure maths for SafeArea: the screen's safe rectangle as RectTransform anchors (0..1).</summary>
    public static class SafeAreaMath
    {
        /// <summary>
        /// Anchors that make a full-screen RectTransform cover only the safe area. An empty or missing screen size
        /// (e.g. before the first frame) gives the full screen, so the UI never collapses.
        /// </summary>
        public static void Anchors(Rect safeArea, float screenWidth, float screenHeight, out Vector2 min, out Vector2 max)
        {
            if (screenWidth <= 0f || screenHeight <= 0f || safeArea.width <= 0f || safeArea.height <= 0f)
            {
                min = Vector2.zero;
                max = Vector2.one;
                return;
            }

            min = new Vector2(Mathf.Clamp01(safeArea.xMin / screenWidth), Mathf.Clamp01(safeArea.yMin / screenHeight));
            max = new Vector2(Mathf.Clamp01(safeArea.xMax / screenWidth), Mathf.Clamp01(safeArea.yMax / screenHeight));
        }
    }
}
