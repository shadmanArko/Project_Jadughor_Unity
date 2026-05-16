using Core.EventBus;
using InputSystem.Data;
using InputSystem.Events;
using InputSystem.Model;
using UniRx;
using UnityEngine;
using Zenject;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.View
{
    /// <summary>
    /// Root container for all mobile input overlays.
    /// Listens to device type and mobile input mode changes, then
    /// shows/hides the VirtualJoystick and TouchGesture sub-views accordingly.
    ///
    /// Hierarchy:
    ///   MobileInputContainer (this script, always in scene)
    ///   ├── MobileVirtualJoystickView   (child — MobileVirtualJoystickView component)
    ///   └── MobileTouchGestureView      (child — MobileTouchGestureView component)
    ///
    /// The entire container is hidden on non-mobile devices.
    /// </summary>
    public sealed class MobileInputContainerView : MonoBehaviour
    {
        // ─── Inspector Fields ──────────────────────────────────────────────────

        [SerializeField] private MobileVirtualJoystickView _virtualJoystickView;
        [SerializeField] private MobileTouchGestureView    _touchGestureView;

        // ─── Dependencies ──────────────────────────────────────────────────────

        private IInputSystemModel _inputModel;
        private EventBus          _eventBus;

        [Inject]
        public void Construct(IInputSystemModel inputModel, EventBus eventBus)
        {
            _inputModel = inputModel;
            _eventBus   = eventBus;
        }

        // ─── Private State ─────────────────────────────────────────────────────

        private readonly CompositeDisposable _disposables = new();

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            // React to device type changes (show container only on mobile/handheld)
            _inputModel.CurrentDeviceType
                .Subscribe(ApplyDeviceVisibility)
                .AddTo(_disposables);

            // React to mobile mode preference changes
            _inputModel.MobileInputMode
                .Subscribe(ApplyMobileMode)
                .AddTo(_disposables);

            // Also react to the event (published after the model is updated)
            _eventBus.Receive<MobileInputModeChangedEvent>()
                .Subscribe(e => ApplyMobileMode(e.CurrentMode))
                .AddTo(_disposables);
        }

        // ─── Logic ────────────────────────────────────────────────────────────

        private void ApplyDeviceVisibility(DeviceType deviceType)
        {
            bool isMobile = deviceType == DeviceType.Mobile || deviceType == DeviceType.Handheld;
            gameObject.SetActive(isMobile);

            if (isMobile)
                ApplyMobileMode(_inputModel.MobileInputMode.Value);
        }

        private void ApplyMobileMode(MobileInputMode mode)
        {
            // Only apply if the container is actually visible
            if (!gameObject.activeInHierarchy) return;

            switch (mode)
            {
                case MobileInputMode.VirtualControls:
                    _virtualJoystickView.Show();
                    _touchGestureView.Hide();
                    break;

                case MobileInputMode.TouchGestures:
                    _virtualJoystickView.Hide();
                    _touchGestureView.Show();
                    break;

                case MobileInputMode.Hybrid:
                    _virtualJoystickView.Show();
                    _touchGestureView.Show();
                    break;
            }
        }

        // ─── Cleanup ──────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
