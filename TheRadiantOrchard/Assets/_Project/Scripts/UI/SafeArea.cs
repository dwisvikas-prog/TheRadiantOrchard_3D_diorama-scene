using UnityEngine;

namespace RadiantOrchard.UI
{
    /// <summary>
    /// Fits this RectTransform to the screen's safe area, so HUD and panels stay clear of notches and camera
    /// cutouts (ENGINEERING_GUIDE 5.8). Put it on a full-screen container; backdrops stay outside it so they still
    /// cover the whole screen. Re-applies when the safe area or resolution changes (rotation, split screen).
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeArea : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _applied;
        private int _width;
        private int _height;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _applied || Screen.width != _width || Screen.height != _height) Apply();
        }

        private void Apply()
        {
            _applied = Screen.safeArea;
            _width = Screen.width;
            _height = Screen.height;
            SafeAreaMath.Anchors(_applied, _width, _height, out Vector2 min, out Vector2 max);
            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
