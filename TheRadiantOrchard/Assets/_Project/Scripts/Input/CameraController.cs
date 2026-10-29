using RadiantOrchard.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace RadiantOrchard.Input
{
    /// <summary>
    /// Drag to pan, pinch to zoom (scroll wheel in the Editor), clamped to the farm bounds and the zoom limits.
    /// A drag only becomes a pan after it moves past a small threshold, so taps and gestures on trees are not stolen;
    /// GestureRecognizer reads IsPanning / IsPinching to know when the camera owns the pointer.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Tooltip("Farm area in world units the view must stay inside. Set by the farm layout.")]
        [SerializeField] private Rect _bounds = new Rect(-24f, -14f, 48f, 28f);

        private Camera _camera;
        private BalanceConfig _balance;

        private bool _pointerDown;
        private bool _pointerIgnored;
        private Vector2 _pointerStart;
        private Vector2 _pointerLast;
        private float _lastPinchDistance;
        private Vector2 _lastPinchMid;

        public bool IsPanning { get; private set; }
        public bool IsPinching { get; private set; }
        public bool OwnsPointer => IsPanning || IsPinching;
        public Rect Bounds => _bounds;

        public void Init(BalanceConfig balance)
        {
            // GameInstaller may call this before this object's Awake has run.
            if (_camera == null) _camera = GetComponent<Camera>();
            _balance = balance;
            _camera.orthographicSize = balance.CameraDefaultOrthoSize;
            ApplyClamp();
        }

        public void SetBounds(Rect bounds)
        {
            _bounds = bounds;
            if (_balance != null) ApplyClamp();
        }

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            if (!_camera.orthographic)
                Log.Error("CameraController: the camera must be orthographic.", this);
        }

        private void OnEnable() => EnhancedTouchSupport.Enable();

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
            ResetPointer();
        }

        private void Update()
        {
            if (_balance == null) return;

            var touches = Touch.activeTouches;
            if (touches.Count >= 2) HandlePinch(touches[0].screenPosition, touches[1].screenPosition);
            else if (touches.Count == 1) HandleDrag(true, touches[0].screenPosition, touches[0].phase == UnityEngine.InputSystem.TouchPhase.Began);
            else if (Mouse.current != null) HandleMouse(Mouse.current);
            else ResetPointer();
        }

        private void HandleMouse(Mouse mouse)
        {
            Vector2 position = mouse.position.ReadValue();
            HandleDrag(mouse.leftButton.isPressed, position, mouse.leftButton.wasPressedThisFrame);

            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f && !IsPointerOverUi())
                ZoomTo(_camera.orthographicSize * (1f - Mathf.Sign(scroll) * _balance.CameraScrollZoomFactor), position);
        }

        private void HandleDrag(bool down, Vector2 position, bool began)
        {
            IsPinching = false;
            _lastPinchDistance = 0f;

            if (!down)
            {
                ResetPointer();
                return;
            }

            if (began || !_pointerDown)
            {
                _pointerDown = true;
                _pointerIgnored = IsPointerOverUi();
                _pointerStart = position;
                _pointerLast = position;
                return;
            }

            if (_pointerIgnored) return;

            if (!IsPanning && (position - _pointerStart).magnitude >= _balance.CameraDragThreshold * Screen.height)
                IsPanning = true;

            if (IsPanning)
            {
                float perPixel = CameraMath.WorldPerPixel(_camera.orthographicSize, Screen.height);
                Vector2 delta = (position - _pointerLast) * perPixel;
                Vector3 p = transform.position;
                transform.position = new Vector3(p.x - delta.x, p.y - delta.y, p.z);
                ApplyClamp();
            }
            _pointerLast = position;
        }

        private void HandlePinch(Vector2 a, Vector2 b)
        {
            IsPanning = false;
            _pointerDown = false;
            float distance = Vector2.Distance(a, b);
            Vector2 mid = (a + b) * 0.5f;

            if (!IsPinching || _lastPinchDistance <= 0f)
            {
                IsPinching = true;
                _lastPinchDistance = distance;
                _lastPinchMid = mid;
                return;
            }

            // Move with the fingers' midpoint, then zoom around it.
            float perPixel = CameraMath.WorldPerPixel(_camera.orthographicSize, Screen.height);
            Vector2 move = (mid - _lastPinchMid) * perPixel;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x - move.x, p.y - move.y, p.z);

            if (distance > 0f)
                ZoomTo(_camera.orthographicSize * (_lastPinchDistance / distance), mid);
            else
                ApplyClamp();

            _lastPinchDistance = distance;
            _lastPinchMid = mid;
        }

        private void ZoomTo(float requestedSize, Vector2 screenFocus)
        {
            float oldSize = _camera.orthographicSize;
            float newSize = CameraMath.ClampOrthoSize(requestedSize, _balance.CameraMinOrthoSize, _balance.CameraMaxOrthoSize, _bounds, _camera.aspect);
            Vector2 camPos = transform.position;
            Vector2 focus = CameraMath.ScreenToWorld(screenFocus, camPos, oldSize, Screen.width, Screen.height);
            Vector2 moved = CameraMath.ZoomAround(camPos, focus, oldSize, newSize);

            _camera.orthographicSize = newSize;
            transform.position = new Vector3(moved.x, moved.y, transform.position.z);
            ApplyClamp();
        }

        private void ApplyClamp()
        {
            float size = CameraMath.ClampOrthoSize(_camera.orthographicSize, _balance.CameraMinOrthoSize, _balance.CameraMaxOrthoSize, _bounds, _camera.aspect);
            _camera.orthographicSize = size;
            Vector2 clamped = CameraMath.ClampPosition(transform.position, size, _camera.aspect, _bounds);
            transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
        }

        private void ResetPointer()
        {
            _pointerDown = false;
            _pointerIgnored = false;
            IsPanning = false;
            IsPinching = false;
            _lastPinchDistance = 0f;
        }

        private static bool IsPointerOverUi() => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(_bounds.center, _bounds.size);
        }
    }
}
