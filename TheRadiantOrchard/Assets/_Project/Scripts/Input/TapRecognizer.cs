using UnityEngine;

namespace RadiantOrchard.Input
{
    /// <summary>
    /// Pure tap rules, fed once per frame with one pointer's button state: a tap is a press and release within
    /// MaxSeconds that never moved MaxMovePixels or more and did not start on the UI. A press and a release in the
    /// same frame (fast clicks, touchpad tap-to-click) still count as a tap.
    /// </summary>
    public sealed class TapRecognizer
    {
        private bool _down;
        private bool _cancelled;
        private Vector2 _start;
        private float _startTime;

        public float MaxSeconds { get; set; }
        public float MaxMovePixels { get; set; }

        /// <summary>Feeds one frame. Returns true when this frame completes a tap.</summary>
        public bool Step(bool pressedThisFrame, bool releasedThisFrame, bool isPressed, Vector2 position, float time, bool pressStartedOverUi)
        {
            if (pressedThisFrame) Press(position, time, pressStartedOverUi);
            // Checked separately from the press: both flags are set when down and up land in one input update.
            if (releasedThisFrame) return Release(position, time);
            if (isPressed && _down && MovedTooFar(position)) _cancelled = true;
            return false;
        }

        /// <summary>A second finger or a lost touch: the current press can no longer become a tap.</summary>
        public void Cancel() => _cancelled = true;

        public void Reset() => _down = false;

        private void Press(Vector2 position, float time, bool overUi)
        {
            _down = true;
            _start = position;
            _startTime = time;
            _cancelled = overUi;
        }

        private bool Release(Vector2 position, float time)
        {
            if (!_down) return false;
            _down = false;
            if (_cancelled || MovedTooFar(position)) return false;
            return time - _startTime <= MaxSeconds;
        }

        private bool MovedTooFar(Vector2 position) => (position - _start).magnitude >= MaxMovePixels;
    }
}
