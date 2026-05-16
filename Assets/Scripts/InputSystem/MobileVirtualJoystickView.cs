using UniRx;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using Zenject;

namespace InputSystem.View
{
    /// <summary>
    /// Manages the Virtual Controls overlay (virtual joystick + action buttons).
    /// Shown/hidden by MobileInputContainerView based on MobileInputMode.
    ///
    /// How it works:
    ///   Each child GameObject carries an OnScreenStick or OnScreenButton component.
    ///   These Unity components synthesise gamepad input at the Input System level,
    ///   so the Move action receives leftStick values and action buttons receive
    ///   buttonSouth etc. — no custom bridging code required.
    ///
    /// Prefab / Hierarchy structure (assemble in the Inspector):
    ///   MobileVirtualJoystickView (this script)
    ///   ├── LeftZone
    ///   │   ├── JoystickBackground (Image)
    ///   │   └── JoystickHandle     (Image + OnScreenStick, path = InputConstants.ControlPaths.VirtualLeftStick)
    ///   └── RightZone
    ///       ├── ButtonSouth  (Image + OnScreenButton, path = InputConstants.ControlPaths.VirtualButtonSouth)
    ///       ├── ButtonEast   (Image + OnScreenButton, path = InputConstants.ControlPaths.VirtualButtonEast)
    ///       ├── ButtonNorth  (Image + OnScreenButton, path = InputConstants.ControlPaths.VirtualButtonNorth)
    ///       └── ButtonWest   (Image + OnScreenButton, path = InputConstants.ControlPaths.VirtualButtonWest)
    ///
    /// Inspector Setup:
    ///   Assign the OnScreenStick component and each OnScreenButton via the
    ///   serialized fields below. Then configure their controlPath in the
    ///   Inspector to match the constants in InputConstants.ControlPaths.
    /// </summary>
    public sealed class MobileVirtualJoystickView : MonoBehaviour
    {
        // ─── Inspector Fields ──────────────────────────────────────────────────

        [Header("Joystick")]
        [Tooltip("OnScreenStick component on the joystick handle. " +
                 "Set its Control Path to '<Gamepad>/leftStick'.")]
        [SerializeField] private OnScreenStick _leftStick;

        [Tooltip("Background image of the joystick. Shown at the touch origin when UseFloatingOrigin is true.")]
        [SerializeField] private RectTransform _joystickBackground;

        [Header("Action Buttons")]
        [SerializeField] private OnScreenButton _buttonSouth;
        [SerializeField] private OnScreenButton _buttonEast;
        [SerializeField] private OnScreenButton _buttonNorth;
        [SerializeField] private OnScreenButton _buttonWest;

        [Header("Joystick Behaviour")]
        [Tooltip("If true, the joystick origin snaps to wherever the player first touches in the left zone.")]
        [SerializeField] private bool _useFloatingOrigin = true;

        // ─── Private State ─────────────────────────────────────────────────────

        private Vector2 _defaultJoystickAnchoredPosition;
        private readonly CompositeDisposable _disposables = new();

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (_joystickBackground != null)
                _defaultJoystickAnchoredPosition = _joystickBackground.anchoredPosition;
        }

        private void Start()
        {
            // Additional runtime behaviour (e.g. floating origin) can be added here.
            // For floating-origin: capture TouchPhase.Began inside the left zone and
            // reposition _joystickBackground before OnScreenStick processes the touch.
            // This is left as a project-specific extension to keep this class generic.
        }

        // ─── Public API (called by MobileInputContainerView) ─────────────────

        /// <summary>Show the virtual controls overlay.</summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the overlay and reset the joystick position to its default.
        /// The OnScreenStick will release its synthesised input automatically.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);

            if (_joystickBackground != null && _useFloatingOrigin)
                _joystickBackground.anchoredPosition = _defaultJoystickAnchoredPosition;
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
