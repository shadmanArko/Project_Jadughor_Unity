using InputSystem.Data;
using InputSystem.Model;
using InputSystem.Utility;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Zenject;
using DeviceType = InputSystem.Data.DeviceType;

namespace InputSystem.View
{
    /// <summary>
    /// Manages the Keyboard / Gamepad / Touch tab strip at the top of the
    /// rebinding screen. Hides tabs for unavailable devices and notifies
    /// RebindingCanvasView when the player switches tabs.
    ///
    /// Prefab structure:
    ///   DeviceTabs (this script)
    ///   ├── KeyboardTabButton  (Button)
    ///   ├── GamepadTabButton   (Button)
    ///   └── TouchTabButton     (Button)
    /// </summary>
    public sealed class DeviceTabView : MonoBehaviour
    {
        // ─── Inspector Fields ──────────────────────────────────────────────────

        [SerializeField] private Button _keyboardTabButton;
        [SerializeField] private Button _gamepadTabButton;
        [SerializeField] private Button _touchTabButton;

        // Optional: visual "selected" indicator on each tab button (swap a colour block, etc.)
        [SerializeField] private Color _activeTabColor   = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color _inactiveTabColor = new(0.7f, 0.7f, 0.7f, 1f);

        // ─── Private State ─────────────────────────────────────────────────────

        private IDeviceModel  _deviceModel;
        private IInputSystemModel _inputModel;

        private readonly CompositeDisposable   _disposables = new();
        private readonly ReactiveProperty<DeviceScheme> _activeScheme
            = new(DeviceScheme.KeyboardMouse);

        /// <summary>Other views (RebindingCanvasView) subscribe to this.</summary>
        public System.IObservable<DeviceScheme> OnSchemeChanged => _activeScheme;
        public DeviceScheme ActiveScheme => _activeScheme.Value;

        // ─── Injection ─────────────────────────────────────────────────────────

        [Inject]
        public void Construct(IDeviceModel deviceModel, IInputSystemModel inputModel)
        {
            _deviceModel = deviceModel;
            _inputModel  = inputModel;
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────────

        private void Start()
        {
            RefreshTabVisibility();

            _keyboardTabButton.OnClickAsObservable()
                .Subscribe(_ => SelectScheme(DeviceScheme.KeyboardMouse))
                .AddTo(_disposables);

            _gamepadTabButton.OnClickAsObservable()
                .Subscribe(_ => SelectScheme(DeviceScheme.Gamepad))
                .AddTo(_disposables);

            _touchTabButton.OnClickAsObservable()
                .Subscribe(_ => SelectScheme(DeviceScheme.Touch))
                .AddTo(_disposables);

            // Select the tab that matches the currently active input device
            _inputModel.CurrentDeviceType
                .Subscribe(SyncTabToDevice)
                .AddTo(_disposables);

            // Apply initial highlight
            UpdateHighlight(_activeScheme.Value);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Force-select a tab programmatically.</summary>
        public void SelectScheme(DeviceScheme scheme)
        {
            _activeScheme.Value = scheme;
            UpdateHighlight(scheme);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────

        private void RefreshTabVisibility()
        {
            _keyboardTabButton.gameObject.SetActive(
                _deviceModel.IsKeyboardConnected || _deviceModel.IsMouseConnected);

            _gamepadTabButton.gameObject.SetActive(_deviceModel.IsGamepadConnected);

            _touchTabButton.gameObject.SetActive(_deviceModel.IsTouchscreenPresent);
        }

        private void SyncTabToDevice(DeviceType deviceType)
        {
            var scheme = deviceType switch
            {
                DeviceType.KeyboardMouse => DeviceScheme.KeyboardMouse,
                DeviceType.Gamepad       => DeviceScheme.Gamepad,
                DeviceType.Mobile        => DeviceScheme.Touch,
                DeviceType.Handheld      => DeviceScheme.Touch,
                _                        => DeviceScheme.KeyboardMouse
            };

            SelectScheme(scheme);
        }

        private void UpdateHighlight(DeviceScheme activeScheme)
        {
            SetTabColor(_keyboardTabButton, activeScheme == DeviceScheme.KeyboardMouse);
            SetTabColor(_gamepadTabButton,  activeScheme == DeviceScheme.Gamepad);
            SetTabColor(_touchTabButton,    activeScheme == DeviceScheme.Touch);
        }

        private void SetTabColor(Button btn, bool isActive)
        {
            var colors       = btn.colors;
            colors.normalColor = isActive ? _activeTabColor : _inactiveTabColor;
            btn.colors       = colors;
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
            _activeScheme.Dispose();
        }
    }
}
