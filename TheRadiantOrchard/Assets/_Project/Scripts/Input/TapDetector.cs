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
    /// Raises Tapped(screen position) for a short press that did not move past the camera's drag threshold, did not
    /// start on the UI and never became a two-finger pinch. Taps on the UI are left to the UI. Editor mouse fallback.
    /// The rules live in TapRecognizer; this component only reads the devices each frame.
    /// </summary>
    public sealed class TapDetector : MonoBehaviour
    {
        private readonly TapRecognizer _recognizer = new TapRecognizer();
        private BalanceConfig _balance;

        public event Action<Vector2> Tapped;

        public void Init(BalanceConfig balance)
        {
            _balance = balance;
            _recognizer.MaxSeconds = balance.TapMaxSeconds;
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            _recognizer.Reset();
        }

        private void Update()
        {
            if (_balance == null) return;
            _recognizer.MaxMovePixels = _balance.CameraDragThreshold * Screen.height;

            var touches = Touch.activeTouches;
            if (touches.Count > 1)
            {
                _recognizer.Cancel();
                return;
            }
            if (touches.Count == 1)
            {
                Touch t = touches[0];
                if (t.phase == TouchPhase.Canceled)
                {
                    _recognizer.Cancel();
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
            if (_recognizer.Step(pressed, released, held, position, Time.unscaledTime, overUi))
                Tapped?.Invoke(position);
        }

        // Pointer id -1 is the mouse; touches use their touch id, as the UI input module does.
        private static bool OverUi(int pointerId) =>
            EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(pointerId);
    }
}
