using UnityEngine;

namespace RadiantOrchard.Input
{
    /// <summary>
    /// Pure gesture rules for one pointer, fed once per frame (ENGINEERING_GUIDE 5.7). Decides what a press is:
    /// a tap, a double tap, a long press, a swipe, or a camera pan. A press that moves past the drag threshold is a
    /// swipe if it is moving fast at that moment, otherwise a pan; a pan never produces a gesture, so camera drags
    /// never harvest (CLAUDE.md 5.1). A swipe that keeps going past SwipeMaxSeconds is handed to the camera.
    /// Distances are in screen pixels; GestureRecognizer converts the BalanceConfig fractions each frame.
    /// </summary>
    public sealed class GestureRules
    {
        // Enough for a 0.4 s swipe at 120 fps with room to spare; once full, the newest point replaces the last.
        private const int MaxPathPoints = 96;

        private readonly Vector2[] _path = new Vector2[MaxPathPoints];
        private int _pathCount;

        private Vector2 _start;
        private Vector2 _last;
        private float _startTime;
        private float _lastTime;

        private bool _hasLastTap;
        private Vector2 _lastTapPosition;
        private float _lastTapTime;
        private bool _followsTap;

        public float TapMaxSeconds { get; set; }
        public float LongPressSeconds { get; set; }
        public float DoubleTapWindowSeconds { get; set; }
        public float SwipeMaxSeconds { get; set; }
        public float DragThresholdPixels { get; set; }
        public float DoubleTapMaxDistancePixels { get; set; }
        public float SwipeMinSpeedPixels { get; set; }
        public float SwipeMinDistancePixels { get; set; }

        public PointerIntent Intent { get; private set; }
        public bool IsPanning => Intent == PointerIntent.Pan;

        /// <summary>Where the last tap, double tap or long press happened (screen pixels).</summary>
        public Vector2 EventPosition { get; private set; }

        /// <summary>The points of the current or just-finished swipe.</summary>
        public SwipePath Path => new SwipePath(_path, _pathCount);

        /// <summary>Feeds one frame. Returns the gesture this frame completed, if any.</summary>
        public GestureEvent Step(bool pressedThisFrame, bool releasedThisFrame, bool isPressed, Vector2 position, float time, bool pressStartedOverUi)
        {
            if (pressedThisFrame) Press(position, time, pressStartedOverUi);
            if (Intent == PointerIntent.None) return GestureEvent.None;
            // Checked separately from the press: both flags are set when down and up land in one input update.
            if (releasedThisFrame) return Release(position, time);
            if (pressedThisFrame) return GestureEvent.None; // Press already recorded this frame's point.
            if (isPressed) return Hold(position, time);

            // The release was never seen (focus lost, device removed): drop the press.
            Intent = PointerIntent.None;
            return GestureEvent.None;
        }

        /// <summary>A second finger or a lost touch: the current press can no longer become a gesture.</summary>
        public void Cancel()
        {
            if (Intent != PointerIntent.None) Intent = PointerIntent.Ignored;
            _hasLastTap = false;
        }

        public void Reset()
        {
            Intent = PointerIntent.None;
            _hasLastTap = false;
        }

        private void Press(Vector2 position, float time, bool overUi)
        {
            _followsTap = _hasLastTap
                && time - _lastTapTime <= DoubleTapWindowSeconds
                && Vector2.Distance(position, _lastTapPosition) <= DoubleTapMaxDistancePixels;
            _hasLastTap = false;

            Intent = overUi ? PointerIntent.Ignored : PointerIntent.Undecided;
            _start = _last = position;
            _startTime = _lastTime = time;
            _pathCount = 0;
            AddPoint(position);
        }

        private GestureEvent Hold(Vector2 position, float time)
        {
            GestureEvent result = GestureEvent.None;
            if (Intent == PointerIntent.Undecided)
            {
                AddPoint(position);
                if (Vector2.Distance(position, _start) >= DragThresholdPixels)
                {
                    Intent = IsFast(position, time) ? PointerIntent.Swipe : PointerIntent.Pan;
                }
                else if (time - _startTime >= LongPressSeconds)
                {
                    Intent = PointerIntent.Held;
                    EventPosition = _start;
                    result = GestureEvent.LongPress;
                }
            }
            else if (Intent == PointerIntent.Swipe)
            {
                AddPoint(position);
                // A flick that turns into a long drag is the player moving the camera after all.
                if (time - _startTime > SwipeMaxSeconds) Intent = PointerIntent.Pan;
            }

            _last = position;
            _lastTime = time;
            return result;
        }

        private GestureEvent Release(Vector2 position, float time)
        {
            PointerIntent intent = Intent;
            Intent = PointerIntent.None;
            float duration = time - _startTime;

            if (intent == PointerIntent.Undecided)
            {
                if (Vector2.Distance(position, _start) < DragThresholdPixels)
                    return duration <= TapMaxSeconds ? TapOrDoubleTap(position, time) : GestureEvent.None;

                // Crossed the drag threshold on the release frame itself: a very quick flick.
                AddPoint(position);
                return IsFast(position, time) && IsSwipeShape(position, duration) ? GestureEvent.Swipe : GestureEvent.None;
            }

            if (intent == PointerIntent.Swipe)
            {
                AddPoint(position);
                return IsSwipeShape(position, duration) ? GestureEvent.Swipe : GestureEvent.None;
            }

            return GestureEvent.None;
        }

        private GestureEvent TapOrDoubleTap(Vector2 position, float time)
        {
            EventPosition = position;
            if (_followsTap && Vector2.Distance(position, _lastTapPosition) <= DoubleTapMaxDistancePixels)
                return GestureEvent.DoubleTap;

            _hasLastTap = true;
            _lastTapPosition = position;
            _lastTapTime = time;
            return GestureEvent.Tap;
        }

        // Speed over the last frame, so a press that rests and then flicks still counts as fast.
        private bool IsFast(Vector2 position, float time)
        {
            float dt = time - _lastTime;
            float speed = dt > 1e-4f
                ? Vector2.Distance(position, _last) / dt
                : Vector2.Distance(position, _start) / Mathf.Max(time - _startTime, 1e-4f);
            return speed >= SwipeMinSpeedPixels;
        }

        private bool IsSwipeShape(Vector2 end, float duration) =>
            Vector2.Distance(end, _start) >= SwipeMinDistancePixels && duration <= SwipeMaxSeconds;

        private void AddPoint(Vector2 point)
        {
            if (_pathCount < MaxPathPoints) _path[_pathCount++] = point;
            else _path[MaxPathPoints - 1] = point;
        }
    }
}
