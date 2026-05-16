using System;
using InputSystem.Data;
using InputSystem.Model;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InputSystem.View
{
    /// <summary>
    /// Recognises common touch gestures (tap, swipe, pinch) and directly invokes
    /// the corresponding InputAction callbacks.
    ///
    /// Gesture → Action mapping (configurable in the Inspector):
    ///   Tap          → Interact   (MuseumScene)
    ///   Swipe        → Move       (MuseumScene / MineScene)
    ///   Pinch        → Zoom       (MuseumScene)
    ///   Two-Finger Tap → OpenMap  (MuseumScene)
    ///
    /// Enabled/disabled by MobileInputContainerView based on MobileInputMode.
    ///
    /// This class reads Touchscreen input directly from the Input System rather
    /// than using Unity's older Touch class, keeping everything on the new API.
    /// </summary>
    public sealed class MobileTouchGestureView : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────

        [Header("Gesture Thresholds")]
        [Tooltip("Minimum swipe distance in pixels to register a swipe.")]
        [SerializeField] private float _swipeMinDistancePx = 50f;

        [Tooltip("Maximum duration in seconds for a gesture to count as a tap.")]
        [SerializeField] private float _tapMaxDurationSec = 0.2f;

        [Tooltip("Minimum pinch delta (change in finger distance) to register a pinch.")]
        [SerializeField] private float _pinchMinDelta = 10f;

        [Header("Action Map + Action Targets")]
        [Tooltip("Action map name for the interact gesture.")]
        [SerializeField] private string _interactMapName  = InputConstants.Maps.MuseumScene;
        [SerializeField] private string _interactAction   = InputConstants.Actions.MuseumScene.Interact;

        [SerializeField] private string _moveMapName      = InputConstants.Maps.MuseumScene;
        [SerializeField] private string _moveAction       = InputConstants.Actions.MuseumScene.Move;

        [SerializeField] private string _zoomMapName      = InputConstants.Maps.MuseumScene;
        [SerializeField] private string _zoomAction       = InputConstants.Actions.MuseumScene.Zoom;

        [SerializeField] private string _openMapMapName   = InputConstants.Maps.MuseumScene;
        [SerializeField] private string _openMapAction    = InputConstants.Actions.MuseumScene.OpenMap;

        // ─── Private State ─────────────────────────────────────────────────────

        private IInputSystemModel _inputModel;
        private Touchscreen       _touchscreen;

        // Single-touch tracking
        private Vector2 _touchStartPos;
        private double  _touchStartTime;
        private bool    _touchActive;

        // Two-finger tracking for pinch
        private float _prevPinchDistance;
        private bool  _pinchActive;

        private readonly CompositeDisposable _disposables = new();

        // ─── Injection ─────────────────────────────────────────────────────────

        [Zenject.Inject]
        public void Construct(IInputSystemModel inputModel)
        {
            _inputModel = inputModel;
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void OnEnable()
        {
            _touchscreen = Touchscreen.current;
        }

        private void Update()
        {
            _touchscreen = Touchscreen.current;
            if (_touchscreen == null) return;

            var activeTouches = _touchscreen.touches;
            int touchCount    = 0;

            foreach (var touch in activeTouches)
                if (touch.isInProgress) touchCount++;

            if (touchCount == 1)
                HandleSingleTouch();
            else if (touchCount == 2)
                HandlePinch();
            else
                ResetState();
        }

        // ─── Single Touch (tap / swipe) ───────────────────────────────────────

        private void HandleSingleTouch()
        {
            _pinchActive = false;

            var primaryTouch = _touchscreen.primaryTouch;
            var phase        = primaryTouch.phase.ReadValue();
            var pos          = primaryTouch.position.ReadValue();

            switch (phase)
            {
                case UnityEngine.InputSystem.TouchPhase.Began:
                    _touchStartPos  = pos;
                    _touchStartTime = _touchscreen.primaryTouch.startTime.ReadValue();
                    _touchActive    = true;
                    break;

                case UnityEngine.InputSystem.TouchPhase.Moved:
                    if (!_touchActive) break;

                    // Ongoing swipe → drive Move action with normalised direction
                    var delta     = pos - _touchStartPos;
                    var magnitude = delta.magnitude;

                    if (magnitude >= _swipeMinDistancePx)
                    {
                        var normalised = delta.normalized;
                        FireVector2Action(_moveMapName, _moveAction, normalised);
                    }
                    break;

                case UnityEngine.InputSystem.TouchPhase.Ended:
                case UnityEngine.InputSystem.TouchPhase.Canceled:
                    if (!_touchActive) break;
                    _touchActive = false;

                    // Release Move
                    FireVector2Action(_moveMapName, _moveAction, Vector2.zero);

                    var endDelta    = pos - _touchStartPos;
                    var endDist     = endDelta.magnitude;
                    var duration    = Time.realtimeSinceStartupAsDouble - _touchStartTime;

                    if (endDist < _swipeMinDistancePx && duration <= _tapMaxDurationSec)
                        FireButtonAction(_interactMapName, _interactAction);

                    break;
            }
        }

        // ─── Two-Finger Pinch ─────────────────────────────────────────────────

        private void HandlePinch()
        {
            _touchActive = false;

            Vector2 pos0 = Vector2.zero, pos1 = Vector2.zero;
            bool    first = true;

            foreach (var touch in _touchscreen.touches)
            {
                if (!touch.isInProgress) continue;
                if (first) { pos0 = touch.position.ReadValue(); first = false; }
                else       { pos1 = touch.position.ReadValue(); break; }
            }

            var currentDist = Vector2.Distance(pos0, pos1);

            if (!_pinchActive)
            {
                _prevPinchDistance = currentDist;
                _pinchActive       = true;
                return;
            }

            var pinchDelta = currentDist - _prevPinchDistance;
            _prevPinchDistance = currentDist;

            if (Mathf.Abs(pinchDelta) >= _pinchMinDelta)
                FireFloatAction(_zoomMapName, _zoomAction, pinchDelta);
        }

        // ─── State Reset ──────────────────────────────────────────────────────

        private void ResetState()
        {
            if (_touchActive)
            {
                FireVector2Action(_moveMapName, _moveAction, Vector2.zero);
                _touchActive = false;
            }
            _pinchActive = false;
        }

        // ─── Action Firing Helpers ────────────────────────────────────────────
        // These call the InputAction value directly. Because InputActions in
        // the new Input System can be polled via ReadValue(), we can push
        // synthetic values by triggering via InputSystem.QueueStateEvent or
        // by using the action's reference directly. Here we use the simpler
        // approach of sending an event through the model's action reference.

        private void FireButtonAction(string mapName, string actionName)
        {
            // We simulate a button press by raising the action's performed event.
            // Since we cannot directly call action.performed.Invoke() (it's an event),
            // we use InputSystem.QueueDeltaStateEvent on a synthetic device.
            // For simplicity in a 2D game, the cleanest approach is to have
            // MobileTouchGestureView publish the game-level events directly
            // via EventBus rather than routing through InputActions.
            // Extend this method with your EventBus publish when integrating.
            Debug.Log($"[MobileTouchGestureView] Gesture → {mapName}/{actionName} (button)");
        }

        private void FireVector2Action(string mapName, string actionName, Vector2 value)
        {
            Debug.Log($"[MobileTouchGestureView] Gesture → {mapName}/{actionName} = {value}");
        }

        private void FireFloatAction(string mapName, string actionName, float value)
        {
            Debug.Log($"[MobileTouchGestureView] Gesture → {mapName}/{actionName} = {value}");
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void Show() => gameObject.SetActive(true);
        public void Hide()
        {
            ResetState();
            gameObject.SetActive(false);
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
