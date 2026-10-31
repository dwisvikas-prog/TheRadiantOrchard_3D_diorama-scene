using System;
using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace RadiantOrchard.Input
{
    /// <summary>
    /// The one reader of single-finger input (touch, or the mouse in the Editor). Feeds GestureRules each frame and
    /// raises Tapped, Gestured (double tap, long press) and Swiped in screen pixels. When the rules decide a press is
    /// a pan, it moves the camera, so taps, gestures and camera drags can never fight over the same press.
    /// Two fingers belong to CameraController (pinch).
    /// </summary>
    public sealed class GestureRecognizer : MonoBehaviour
    {
        private readonly GestureRules _rules = new GestureRules();
        private BalanceConfig _balance;
        private CameraController _camera;
        private Vector2 _lastPosition;
        private bool _locked;
        // After a lock ends, input stays ignored until every finger (or the mouse button) is up, so a press that was
        // held through a scripted sequence never turns into a gesture or a pan.
        private bool _waitForRelease;

        public event Action<Vector2> Tapped;
        public event Action<GestureType, Vector2> Gestured;
        public event Action<SwipePath> Swiped;

        public void Init(BalanceConfig balance, CameraController cameraController)
        {
            _balance = balance;
            _camera = cameraController;
        }

        /// <summary>
        /// While locked (a scripted sequence such as the zone unlock), no taps, gestures or pans are recognised; locking
        /// cancels a gesture in progress.
        /// </summary>
        public bool Locked
        {
            get => _locked;
            set
            {
                if (value == _locked) return;
                _locked = value;
                _rules.Cancel();
                if (!value) _waitForRelease = true;
            }
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _rules.Reset();
        }

        private void Update()
        {
            if (_balance == null || _locked) return;
            if (_waitForRelease)
            {
                if (Touch.activeTouches.Count > 0 || (Mouse.current != null && Mouse.current.leftButton.isPressed)) return;
                _waitForRelease = false;
            }
            ApplyTuning();

            var touches = Touch.activeTouches;
            if (touches.Count > 1)
            {
                _rules.Cancel();
                return;
            }
            if (touches.Count == 1)
            {
                Touch t = touches[0];
                if (t.phase == TouchPhase.Canceled)
                {
                    _rules.Cancel();
                    return;
                }
                bool began = t.phase == TouchPhase.Began;
                Feed(began, t.phase == TouchPhase.Ended, t.isInProgress, t.screenPosition, began && OverUi(t.touchId));
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            bool pressed = mouse.leftButton.wasPressedThisFrame;
            Feed(pressed, mouse.leftButton.wasReleasedThisFrame, mouse.leftButton.isPressed, mouse.position.ReadValue(), pressed && OverUi(-1));
        }

        private void Feed(bool pressed, bool released, bool held, Vector2 position, bool overUi)
        {
            if (pressed) _lastPosition = position;
            GestureEvent e = _rules.Step(pressed, released, held, position, Time.unscaledTime, overUi);

            if (_rules.IsPanning && held && _camera != null) _camera.PanByScreenDelta(position - _lastPosition);
            _lastPosition = position;

            switch (e)
            {
                case GestureEvent.Tap: Tapped?.Invoke(_rules.EventPosition); break;
                case GestureEvent.DoubleTap: Gestured?.Invoke(GestureType.DoubleTap, _rules.EventPosition); break;
                case GestureEvent.LongPress: Gestured?.Invoke(GestureType.LongPress, _rules.EventPosition); break;
                case GestureEvent.Swipe: Swiped?.Invoke(_rules.Path); break;
            }
        }

        // Fractions of screen height from BalanceConfig, so gestures feel the same on every screen size.
        private void ApplyTuning()
        {
            float h = Screen.height;
            _rules.TapMaxSeconds = _balance.TapMaxSeconds;
            _rules.LongPressSeconds = _balance.LongPressSeconds;
            _rules.DoubleTapWindowSeconds = _balance.DoubleTapWindowSeconds;
            _rules.SwipeMaxSeconds = _balance.SwipeMaxSeconds;
            _rules.DragThresholdPixels = _balance.CameraDragThreshold * h;
            _rules.DoubleTapMaxDistancePixels = _balance.DoubleTapMaxDistance * h;
            _rules.SwipeMinSpeedPixels = _balance.SwipeMinSpeed * h;
            _rules.SwipeMinDistancePixels = _balance.SwipeMinDistance * h;
        }

        // Pointer id -1 is the mouse; touches use their touch id, as the UI input module does.
        private static bool OverUi(int pointerId) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
    }
}
